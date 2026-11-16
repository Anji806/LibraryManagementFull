using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Tests;

/// <summary>The borrowing rules themselves (no web host, no database).</summary>
public class BorrowingPolicyTests
{
    private static BorrowingPolicy Policy(Action<BorrowingOptions>? configure = null)
    {
        var options = new BorrowingOptions();
        configure?.Invoke(options);
        return new BorrowingPolicy(Options.Create(options));
    }

    private static DateTime Utc(int month, int day, int hour = 0, int minute = 0)
        => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    // 1 Nov 10:00 UTC is 1 Nov 15:30 in India; the 14-day period ends on 15 Nov.
    private static readonly DateTime Borrowed = Utc(11, 1, 10);

    [Fact]
    public void DueDate_IsBorrowDayPlusFourteenDays_EndingAtTheEndOfThatDayInIndia()
    {
        var due = Policy().ComputeDueDate(Borrowed);

        Assert.Equal(DateTimeKind.Utc, due.Kind);
        // 15 Nov 23:59:59.9999999 IST = 15 Nov 18:29:59.9999999 UTC
        Assert.Equal(new DateTime(2026, 11, 15, 18, 29, 59, DateTimeKind.Utc).AddTicks(9_999_999), due);
    }

    [Fact]
    public void BorrowedJustAfterLocalMidnight_CountsFromTheLocalDay()
    {
        // 18:35 UTC on 1 Nov is already 2 Nov (00:05) in India -> due 16 Nov.
        var due = Policy().ComputeDueDate(Utc(11, 1, 18, 35));

        Assert.Equal(Utc(11, 16, 18, 29), due.AddTicks(-due.Ticks % TimeSpan.TicksPerMinute));
    }

    [Theory]
    [InlineData(11, 10, 12, 0, 0)]   // well before the due date
    [InlineData(11, 15, 18, 0, 0)]   // 23:30 IST on the due date: still on time
    [InlineData(11, 15, 19, 0, 1)]   // 00:30 IST the next day: one day late
    [InlineData(11, 16, 12, 0, 1)]   // 16 Nov
    [InlineData(11, 19, 5, 0, 4)]    // 19 Nov: four days late
    public void Fine_IsFiveRupeesPerCalendarDayAfterTheDueDate(
        int month, int day, int hour, int minute, int expectedDays)
    {
        var policy = Policy();
        var due = policy.ComputeDueDate(Borrowed);
        var returned = Utc(month, day, hour, minute);

        Assert.Equal(expectedDays, policy.OverdueDays(due, returned));
        Assert.Equal(expectedDays * 5m, policy.CalculateFine(due, returned));
    }

    [Fact]
    public void Fine_UsesDecimalArithmetic_AndConfigurableRate()
    {
        var policy = Policy(o => o.FinePerDay = 7.5m);
        var due = policy.ComputeDueDate(Borrowed);

        Assert.Equal(22.5m, policy.CalculateFine(due, Utc(11, 18, 12)));
    }

    [Fact]
    public void LoanPeriod_IsConfigurable()
    {
        var due = Policy(o => o.LoanPeriodDays = 7).ComputeDueDate(Borrowed);

        Assert.Equal(new DateOnly(2026, 11, 8), DateOnly.FromDateTime(due.AddHours(5.5)));
    }

    [Fact]
    public void InvalidConfiguration_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Policy(o => o.LoanPeriodDays = 0));
        Assert.Throws<InvalidOperationException>(() => Policy(o => o.FinePerDay = -1));
        Assert.Throws<InvalidOperationException>(() => Policy(o => o.TimeZoneId = "Not/AZone"));
    }

    private static BorrowTransaction Active(BorrowingPolicy policy)
        => new() { BorrowDate = Borrowed, DueDate = policy.ComputeDueDate(Borrowed), Status = "Borrowed" };

    [Fact]
    public void ActiveLoan_StatusMovesFromBorrowedToDueSoonToOverdue()
    {
        var policy = Policy();
        var loan = Active(policy);

        var early = policy.Evaluate(loan, Utc(11, 5, 12));
        Assert.Equal("Borrowed", early.DisplayStatus);
        Assert.Equal(10, early.DaysRemaining);

        var soon = policy.Evaluate(loan, Utc(11, 14, 12));
        Assert.Equal("Due Soon", soon.DisplayStatus);
        Assert.Equal(1, soon.DaysRemaining);

        var onDay = policy.Evaluate(loan, Utc(11, 15, 18));
        Assert.Equal("Due Soon", onDay.DisplayStatus);
        Assert.Equal(0, onDay.DaysRemaining);
        Assert.Equal(0m, onDay.Fine);

        var late = policy.Evaluate(loan, Utc(11, 19, 5));
        Assert.True(late.IsOverdue);
        Assert.Equal("Overdue", late.DisplayStatus);
        Assert.Equal(4, late.OverdueDays);
        Assert.Equal(20m, late.Fine);
    }

    [Fact]
    public void ReturnedLoan_UsesItsStoredFine_AndShowsFinePendingUntilPaid()
    {
        var policy = Policy();
        var loan = Active(policy);
        loan.ReturnDate = Utc(11, 19, 5);
        loan.Status = "Returned";
        loan.FineAmount = 20m;

        var owing = policy.Evaluate(loan, Utc(12, 30));
        Assert.Equal("Fine Pending", owing.DisplayStatus);
        Assert.Equal(20m, owing.Fine);
        Assert.False(owing.IsOverdue);

        loan.FinePaid = 20m;
        Assert.Equal("Returned", policy.Evaluate(loan, Utc(12, 30)).DisplayStatus);

        loan.FineAmount = 0m;
        loan.FinePaid = 0m;
        Assert.Equal("Returned", policy.Evaluate(loan, Utc(12, 30)).DisplayStatus);
    }

    [Fact]
    public void LoanWithoutDueDate_IsNeverOverdueAndOwesNothing()
    {
        var policy = Policy();
        var legacy = new BorrowTransaction { BorrowDate = Utc(1, 1), Status = "Borrowed" };

        var status = policy.Evaluate(legacy, Utc(11, 30));

        Assert.False(status.IsOverdue);
        Assert.Equal(0m, status.Fine);
        Assert.Equal("Borrowed", status.DisplayStatus);
        Assert.Null(status.DaysRemaining);
    }
}

/// <summary>Borrow / return with deadlines and fines, through the real API.</summary>
public class BorrowingFinesApiTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";

    private readonly LibraryApiFactory _factory;

    public BorrowingFinesApiTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    private BorrowingPolicy Policy => _factory.Services.GetRequiredService<BorrowingPolicy>();

    private sealed record Auth(string Token, int UserId);

    private async Task<(HttpClient Client, int UserId)> SignInAsync(string role)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = role, email, password = Password, deviceId = "fines" })).EnsureSuccessStatusCode();

        if (role != "Member")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password, deviceId = "fines" });
        login.EnsureSuccessStatusCode();

        var auth = (await login.Content.ReadFromJsonAsync<Auth>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.UserId);
    }

    private async Task<int> SeedBookAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N");

        var book = new Book
        {
            Title = "Book " + suffix,
            ISBN = suffix[..13],
            TotalCopies = 2,
            AvailableCopies = 2,
            PublishedYear = 2024,
            Author = new Author { AuthorName = "Author " + suffix },
            Category = new Category { CategoryName = "Category " + suffix }
        };

        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book.BookId;
    }

    /// <summary>Member borrows a fresh book; returns the transaction id.</summary>
    private async Task<int> BorrowAsync(HttpClient client, int memberId)
    {
        var bookId = await SeedBookAsync();
        var response = await client.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("transaction").GetProperty("transactionId").GetInt32();
    }

    /// <summary>Makes a loan look as if it was borrowed <paramref name="daysAgo"/> days ago.</summary>
    private async Task BackdateAsync(int transactionId, int daysAgo, bool keepDueDate = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var borrowed = DateTime.UtcNow.AddDays(-daysAgo);
        DateTime? due = keepDueDate ? null : Policy.ComputeDueDate(borrowed);

        await db.BorrowTransactions.Where(t => t.TransactionId == transactionId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(t => t.BorrowDate, borrowed)
                .SetProperty(t => t.DueDate, due));
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Borrow_SavesBorrowDateAndFourteenDayDueDate_OnTheServer()
    {
        var (member, memberId) = await SignInAsync("Member");
        var bookId = await SeedBookAsync();

        var response = await member.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}&borrowDate=2000-01-01&dueDate=2099-01-01",
            null);
        var transaction = (await ReadAsync(response)).GetProperty("transaction");

        var borrowed = transaction.GetProperty("borrowDate").GetDateTime();
        var due = transaction.GetProperty("dueDate").GetDateTime();

        Assert.True(Math.Abs((DateTime.UtcNow - borrowed.ToUniversalTime()).TotalMinutes) < 5);
        Assert.Equal(Policy.ComputeDueDate(borrowed.ToUniversalTime()), due.ToUniversalTime());
        Assert.InRange((due - borrowed).TotalDays, 13.0, 15.0);
        Assert.Equal("Borrowed", transaction.GetProperty("displayStatus").GetString());
        Assert.Equal(0m, transaction.GetProperty("currentFine").GetDecimal());

        // Persisted, and not influenced by anything the client sent.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.BorrowTransactions.SingleAsync(
            t => t.TransactionId == transaction.GetProperty("transactionId").GetInt32());
        Assert.NotNull(saved.DueDate);
        Assert.Equal(0m, saved.FineAmount);
        Assert.Equal(0m, saved.FinePaid);
    }

    [Theory]
    [InlineData(-3, 0)]   // returned before the due date
    [InlineData(0, 0)]    // returned on the due date
    [InlineData(1, 5)]    // one day late
    [InlineData(4, 20)]   // four days late
    public async Task Return_CalculatesTheFinalFineOnTheServer(int daysLate, int expectedFine)
    {
        var (member, memberId) = await SignInAsync("Member");
        var id = await BorrowAsync(member, memberId);
        await BackdateAsync(id, daysAgo: 14 + daysLate);

        // A client-supplied fine is ignored: the endpoint reads no body.
        var response = await member.PutAsJsonAsync(
            $"/api/borrowtransactions/return/{id}", new { fineAmount = 0, returnDate = "2000-01-01" });
        var transaction = (await ReadAsync(response)).GetProperty("transaction");

        Assert.Equal(expectedFine, transaction.GetProperty("fineAmount").GetDecimal());
        Assert.Equal(expectedFine, transaction.GetProperty("currentFine").GetDecimal());
        Assert.Equal(expectedFine, transaction.GetProperty("outstandingFine").GetDecimal());
        Assert.Equal(expectedFine > 0 ? "Fine Pending" : "Returned",
            transaction.GetProperty("displayStatus").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.BorrowTransactions.SingleAsync(t => t.TransactionId == id);
        Assert.Equal(expectedFine, saved.FineAmount);
        Assert.Equal(0m, saved.FinePaid); // never marked paid automatically
        Assert.NotNull(saved.ReturnDate);
        Assert.Equal("Returned", saved.Status);
    }

    [Fact]
    public async Task ActiveOverdueLoan_ShowsOverdueStatusAndRunningFine_WithoutStoringIt()
    {
        var (member, memberId) = await SignInAsync("Member");
        var id = await BorrowAsync(member, memberId);
        await BackdateAsync(id, daysAgo: 20);   // due 6 days ago

        var list = await ReadAsync(await member.GetAsync($"/api/members/{memberId}/borrowed-books"));
        var loan = list.EnumerateArray().Single(t => t.GetProperty("transactionId").GetInt32() == id);

        Assert.True(loan.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(6, loan.GetProperty("overdueDays").GetInt32());
        Assert.Equal(30m, loan.GetProperty("currentFine").GetDecimal());
        Assert.Equal(30m, loan.GetProperty("outstandingFine").GetDecimal());
        Assert.Equal("Overdue", loan.GetProperty("displayStatus").GetString());
        Assert.Equal(0m, loan.GetProperty("fineAmount").GetDecimal()); // final fine only on return

        // Admin-side views use the same calculation (book borrow history).
        var (librarian, _) = await SignInAsync("Librarian");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookId = await db.BorrowTransactions.Where(t => t.TransactionId == id)
            .Select(t => t.BookId).SingleAsync();
        var history = await ReadAsync(await librarian.GetAsync($"/api/books/{bookId}/borrow-history"));
        Assert.Equal(30m, history[0].GetProperty("currentFine").GetDecimal());
    }

    [Fact]
    public async Task ActiveLoanNearItsDueDate_IsDueSoon()
    {
        var (member, memberId) = await SignInAsync("Member");
        var id = await BorrowAsync(member, memberId);
        await BackdateAsync(id, daysAgo: 13);

        var list = await ReadAsync(await member.GetAsync($"/api/members/{memberId}/borrowed-books"));
        var loan = list.EnumerateArray().Single(t => t.GetProperty("transactionId").GetInt32() == id);

        Assert.Equal("Due Soon", loan.GetProperty("displayStatus").GetString());
        Assert.Equal(1, loan.GetProperty("daysRemaining").GetInt32());
        Assert.False(loan.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task LoanFromBeforeDueDatesExisted_IsNeverOverdueAndReturnsWithoutFine()
    {
        var (member, memberId) = await SignInAsync("Member");
        var id = await BorrowAsync(member, memberId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.BorrowTransactions.Where(t => t.TransactionId == id).ExecuteUpdateAsync(set => set
                .SetProperty(t => t.BorrowDate, DateTime.UtcNow.AddDays(-400))
                .SetProperty(t => t.DueDate, (DateTime?)null));
        }

        var list = await ReadAsync(await member.GetAsync($"/api/members/{memberId}/borrowed-books"));
        var loan = list.EnumerateArray().Single(t => t.GetProperty("transactionId").GetInt32() == id);
        Assert.False(loan.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(0m, loan.GetProperty("currentFine").GetDecimal());

        var returned = (await ReadAsync(await member.PutAsync($"/api/borrowtransactions/return/{id}", null)))
            .GetProperty("transaction");
        Assert.Equal(0m, returned.GetProperty("fineAmount").GetDecimal());
    }

    [Fact]
    public async Task ReturnedLateLoan_ShowsFinePendingInHistory_UntilPaid()
    {
        var (member, memberId) = await SignInAsync("Member");
        var id = await BorrowAsync(member, memberId);
        await BackdateAsync(id, daysAgo: 18);   // 4 days late
        (await member.PutAsync($"/api/borrowtransactions/return/{id}", null)).EnsureSuccessStatusCode();

        async Task<JsonElement> HistoryEntryAsync()
        {
            var history = await ReadAsync(await member.GetAsync($"/api/members/{memberId}/borrow-history"));
            return history.EnumerateArray().Single(t => t.GetProperty("transactionId").GetInt32() == id);
        }

        var entry = await HistoryEntryAsync();
        Assert.Equal("Fine Pending", entry.GetProperty("displayStatus").GetString());
        Assert.Equal(20m, entry.GetProperty("fineAmount").GetDecimal());
        Assert.Equal(0m, entry.GetProperty("finePaid").GetDecimal());
        Assert.Equal(20m, entry.GetProperty("outstandingFine").GetDecimal());

        // A later payment feature records FinePaid; the balance follows.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.BorrowTransactions.Where(t => t.TransactionId == id)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.FinePaid, 20m));
        }

        entry = await HistoryEntryAsync();
        Assert.Equal("Returned", entry.GetProperty("displayStatus").GetString());
        Assert.Equal(0m, entry.GetProperty("outstandingFine").GetDecimal());
    }

    [Fact]
    public async Task DuplicateReturn_IsRejected_AndDoesNotChangeTheFine()
    {
        var (member, memberId) = await SignInAsync("Member");
        var id = await BorrowAsync(member, memberId);
        await BackdateAsync(id, daysAgo: 16);   // 2 days late
        (await member.PutAsync($"/api/borrowtransactions/return/{id}", null)).EnsureSuccessStatusCode();

        var again = await member.PutAsync($"/api/borrowtransactions/return/{id}", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(10m, (await db.BorrowTransactions.SingleAsync(t => t.TransactionId == id)).FineAmount);
    }

    [Fact]
    public async Task OneMember_CannotReturnAnotherMembersBook()
    {
        var (owner, ownerId) = await SignInAsync("Member");
        var (other, _) = await SignInAsync("Member");
        var id = await BorrowAsync(owner, ownerId);

        var response = await other.PutAsync($"/api/borrowtransactions/return/{id}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.BorrowTransactions.SingleAsync(t => t.TransactionId == id);
        Assert.Null(saved.ReturnDate);
        Assert.Equal("Borrowed", saved.Status);
    }

    [Fact]
    public async Task Return_StillRestoresBookAvailability()
    {
        var (member, memberId) = await SignInAsync("Member");
        var bookId = await SeedBookAsync();

        var borrow = await ReadAsync(await member.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null));
        var id = borrow.GetProperty("transaction").GetProperty("transactionId").GetInt32();

        async Task<int> AvailableAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (await db.Books.SingleAsync(b => b.BookId == bookId)).AvailableCopies;
        }

        Assert.Equal(1, await AvailableAsync());
        (await member.PutAsync($"/api/borrowtransactions/return/{id}", null)).EnsureSuccessStatusCode();
        Assert.Equal(2, await AvailableAsync());
    }
}
