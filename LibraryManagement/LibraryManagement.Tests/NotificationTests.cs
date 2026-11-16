using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Services;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LibraryManagement.Tests;

/// <summary>Due reminders, overdue notices and availability notifications.</summary>
public class NotificationTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";

    /// <summary>Stands in for Gmail so every send can be observed (and made to fail).</summary>
    private sealed class FakeEmail : IEmailService
    {
        public static readonly List<(string To, string Subject, string Body)> Sent = new();
        public static readonly HashSet<string> FailFor = new();

        public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            lock (FailFor)
            {
                if (FailFor.Contains(toEmail))
                {
                    throw new EmailDeliveryException(EmailFailure.ConnectionFailed, "Could not reach the mail server.");
                }
            }

            lock (Sent) Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private readonly LibraryApiFactory _baseFactory;

    public NotificationTests(LibraryApiFactory factory)
    {
        _baseFactory = factory;
    }

    private WebApplicationFactory<Program> Host(Dictionary<string, string?>? extra = null, bool emailConfigured = true)
        => _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Notifications:Enabled"] = "false", // tests run the scans themselves
                    ["Notifications:SendAfterLocalHour"] = "0"
                };

                // Always set explicitly: in Development the developer's real user
                // secrets would otherwise leak into the test host.
                values["EmailSettings:SenderEmail"] = emailConfigured ? "library@test.local" : "";
                values["EmailSettings:Password"] = emailConfigured ? "app-password" : "";

                foreach (var (key, value) in extra ?? new()) values[key] = value;
                config.AddInMemoryCollection(values);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService, FakeEmail>();
            });
        });

    // ---------------------------------------------------------------- helpers

    private sealed record Auth(string Token, int UserId);

    private static async Task<(HttpClient Client, int UserId, string Email)> SignInAsync(
        WebApplicationFactory<Program> host, string role)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        var client = host.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = $"{role} Reader", email, password = Password, deviceId = "n" })).EnsureSuccessStatusCode();

        if (role != "Member")
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password, deviceId = "n" });
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<Auth>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.UserId, email);
    }

    private static async Task<int> SeedBookAsync(WebApplicationFactory<Program> host, int total = 1, int? available = null)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var book = new Book
        {
            Title = "Title " + suffix[..8],
            ISBN = suffix[..13],
            TotalCopies = total,
            AvailableCopies = available ?? total,
            PublishedYear = 2020,
            Author = new Author { AuthorName = "Author" },
            Category = new Category { CategoryName = "Category " + suffix[..6] }
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book.BookId;
    }

    /// <summary>Creates a loan borrowed <paramref name="borrowedDaysAgo"/> days ago (due 14 days after that).</summary>
    private static async Task<int> SeedLoanAsync(
        WebApplicationFactory<Program> host, int userId, int bookId, int borrowedDaysAgo,
        DateTime? now = null, bool returned = false)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<BorrowingPolicy>();
        var borrowed = (now ?? DateTime.UtcNow).AddDays(-borrowedDaysAgo);

        var loan = new BorrowTransaction
        {
            BookId = bookId,
            MemberId = userId,
            BorrowDate = borrowed,
            DueDate = policy.ComputeDueDate(borrowed),
            ReturnDate = returned ? DateTime.UtcNow : null,
            Status = returned ? "Returned" : "Borrowed"
        };
        db.BorrowTransactions.Add(loan);
        await db.SaveChangesAsync();
        return loan.TransactionId;
    }

    private static async Task<LoanScanSummary> ScanAsync(
        WebApplicationFactory<Program> host, DateTime? now = null, bool ignoreWindow = true)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ILoanNotificationService>()
            .RunAsync(now, ignoreWindow, CancellationToken.None);
    }

    private static List<(string To, string Subject, string Body)> MailTo(string email)
    {
        lock (FakeEmail.Sent) return FakeEmail.Sent.Where(m => m.To == email).ToList();
    }

    private static async Task<List<(string To, string Subject, string Body)>> WaitForMailAsync(string email, int count)
    {
        for (var i = 0; i < 100; i++)
        {
            var mails = MailTo(email);
            if (mails.Count >= count) return mails;
            await Task.Delay(50);
        }

        return MailTo(email);
    }

    private static async Task<T> InDbAsync<T>(WebApplicationFactory<Program> host, Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = host.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    // ================================================================ due reminders

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    public async Task ABookDueInTwoOrOneDays_GetsAReminder(int daysLeft)
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host);
        var title = await InDbAsync(host, db => db.Books.Where(b => b.BookId == bookId).Select(b => b.Title).SingleAsync());
        await SeedLoanAsync(host, userId, bookId, borrowedDaysAgo: 14 - daysLeft);

        var summary = await ScanAsync(host);

        Assert.Equal(1, summary.DueReminders);
        var mail = Assert.Single(MailTo(email));
        Assert.Equal("Reminder: Your Library Book Is Due Soon", mail.Subject);
        Assert.Contains(title, mail.Body);
        Assert.Contains("Reader", mail.Body);                       // the member's name
        Assert.Contains($"{daysLeft} day", mail.Body);
        Assert.Contains("Issued on", mail.Body);
        Assert.Contains("Due date", mail.Body);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    [InlineData(0)]
    public async Task OtherDueDates_GetNoReminder(int daysLeft)
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 14 - daysLeft);

        await ScanAsync(host);

        Assert.Empty(MailTo(email));
    }

    [Fact]
    public async Task AReturnedBook_GetsNoReminder()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12, returned: true);

        await ScanAsync(host);

        Assert.Empty(MailTo(email));
    }

    [Fact]
    public async Task ReminderDays_AreConfigurable()
    {
        using var host = Host(new() { ["Notifications:DueReminderDaysBefore:0"] = "3", ["Notifications:DueReminderDaysBefore:1"] = "0" });
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 11); // 3 days left

        await ScanAsync(host);

        Assert.Single(MailTo(email));
    }

    // ================================================================ overdue

    [Fact]
    public async Task AnOverdueBook_GetsAnOverdueNotice_WithDaysAndTheExistingFine()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 17); // 3 days overdue

        var summary = await ScanAsync(host);

        Assert.Equal(1, summary.OverdueNotices);
        var mail = Assert.Single(MailTo(email));
        Assert.Equal("Overdue Book Notification", mail.Subject);
        Assert.Contains("Days overdue", mail.Body);
        Assert.Contains(">3<", mail.Body);
        Assert.Contains("₹15.00", mail.Body);   // 3 days x ₹5: the existing BorrowingPolicy rule
        Assert.Contains("return the book", mail.Body);
    }

    [Fact]
    public async Task OverdueNotices_RepeatDaily_AndTheFineGrows()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 15); // 1 day overdue
        var now = DateTime.UtcNow;

        await ScanAsync(host, now);
        await ScanAsync(host, now.AddDays(1));
        await ScanAsync(host, now.AddDays(2));

        var mails = MailTo(email);
        Assert.Equal(3, mails.Count);
        Assert.Contains("₹5.00", mails[0].Body);
        Assert.Contains("₹10.00", mails[1].Body);
        Assert.Contains("₹15.00", mails[2].Body);
    }

    [Fact]
    public async Task OverdueFrequency_IsConfigurable()
    {
        using var host = Host(new() { ["Notifications:OverdueEveryDays"] = "3" });
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 15); // overdue day 1
        var now = DateTime.UtcNow;

        for (var day = 0; day < 6; day++) await ScanAsync(host, now.AddDays(day));

        // overdue days 1 and 4 only
        Assert.Equal(2, MailTo(email).Count);
    }

    [Fact]
    public async Task OnceTheBookIsReturned_NoMoreNoticesAreSent()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        var loanId = await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 16);
        var now = DateTime.UtcNow;

        await ScanAsync(host, now);
        Assert.Single(MailTo(email));

        await InDbAsync(host, db => db.BorrowTransactions.Where(t => t.TransactionId == loanId)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.ReturnDate, DateTime.UtcNow)));

        await ScanAsync(host, now.AddDays(1));
        await ScanAsync(host, now.AddDays(2));

        Assert.Single(MailTo(email));
    }

    // ================================================================ duplicates

    [Fact]
    public async Task RunningTheScanAgain_NeverSendsTheSameReminderTwice()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12);

        await ScanAsync(host);
        await ScanAsync(host);
        await ScanAsync(host);

        Assert.Single(MailTo(email));
        Assert.Equal(1, await InDbAsync(host, db => db.Notifications.CountAsync(n => n.UserId == userId)));
    }

    [Fact]
    public async Task ARestart_DoesNotRepeatNotifications()
    {
        using var first = Host();
        var (_, userId, email) = await SignInAsync(first, "Member");
        await SeedLoanAsync(first, userId, await SeedBookAsync(first), borrowedDaysAgo: 12);
        await ScanAsync(first);
        Assert.Single(MailTo(email));

        // A brand-new application instance with no memory of the earlier run
        // (it shares the database, as a restarted server would).
        using var restarted = Host();
        await ScanAsync(restarted);
        await ScanAsync(restarted);

        Assert.Single(MailTo(email));
    }

    [Fact]
    public async Task OverlappingRuns_SendExactlyOnce()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ScanAsync(host)));

        Assert.Single(MailTo(email));
        Assert.Equal(1, await InDbAsync(host, db => db.Notifications.CountAsync(n => n.UserId == userId && n.Status == NotificationStatuses.Sent)));
    }

    // ================================================================ failures

    [Fact]
    public async Task AnEmailFailure_IsRecorded_ThenRetriedSafely_AndSentOnce()
    {
        using var host = Host(new() { ["Notifications:RetryDelayMinutes"] = "30" });
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12);
        lock (FakeEmail.FailFor) FakeEmail.FailFor.Add(email);

        var failed = await ScanAsync(host);

        Assert.Equal(1, failed.Failed);
        Assert.Empty(MailTo(email));
        var row = await InDbAsync(host, db => db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == userId));
        Assert.Equal(NotificationStatuses.Failed, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Null(row.SentAt);                                   // never "sent" without confirmation
        Assert.Contains("ConnectionFailed", row.ErrorMessage);

        // Gmail is back, but the retry delay has not passed: nothing is attempted yet.
        lock (FakeEmail.FailFor) FakeEmail.FailFor.Remove(email);
        await ScanAsync(host);
        Assert.Empty(MailTo(email));

        // After the delay it is retried, succeeds, and is sent exactly once.
        await InDbAsync(host, db => db.Notifications.Where(n => n.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.LastAttemptAt, DateTime.UtcNow.AddMinutes(-31))));
        await ScanAsync(host);
        await ScanAsync(host);

        Assert.Single(MailTo(email));
        row = await InDbAsync(host, db => db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == userId));
        Assert.Equal(NotificationStatuses.Sent, row.Status);
        Assert.Equal(2, row.Attempts);
        Assert.NotNull(row.SentAt);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public async Task AfterTheMaximumAttempts_ItGivesUp()
    {
        using var host = Host(new() { ["Notifications:MaxAttempts"] = "2", ["Notifications:RetryDelayMinutes"] = "0" });
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12);
        lock (FakeEmail.FailFor) FakeEmail.FailFor.Add(email);

        for (var i = 0; i < 5; i++) await ScanAsync(host);

        var row = await InDbAsync(host, db => db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == userId));
        Assert.Equal(NotificationStatuses.Failed, row.Status);
        Assert.Equal(2, row.Attempts); // not 5: it stopped
    }

    [Fact]
    public async Task ACrashedSender_LeavesAStalePendingRow_ThatIsRetried_ButAFreshOneIsLeftAlone()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host);
        var loanId = await SeedLoanAsync(host, userId, bookId, borrowedDaysAgo: 12);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Another worker claimed it a moment ago and is still sending.
        await InDbAsync(host, async db =>
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId, BookId = bookId, TransactionId = loanId,
                NotificationType = NotificationTypes.DueReminder, DedupeKey = $"due:{loanId}:2",
                NotificationDate = today, Status = NotificationStatuses.Pending, Attempts = 1,
                CreatedAt = DateTime.UtcNow, LastAttemptAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await ScanAsync(host);
        Assert.Empty(MailTo(email));

        // That worker died 11 minutes ago: the notification is retried.
        await InDbAsync(host, db => db.Notifications.Where(n => n.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.LastAttemptAt, DateTime.UtcNow.AddMinutes(-11))));
        await ScanAsync(host);

        Assert.Single(MailTo(email));
    }

    // ================================================================ scheduling rules

    [Fact]
    public async Task WithoutEmailConfiguration_TheScanIsSkippedAndNothingIsRecorded()
    {
        using var host = Host(emailConfigured: false);
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12);

        var summary = await ScanAsync(host);

        Assert.True(summary.SkippedEmailNotConfigured);
        Assert.Empty(MailTo(email));
        Assert.Equal(0, await InDbAsync(host, db => db.Notifications.CountAsync(n => n.UserId == userId)));
    }

    [Fact]
    public async Task EmailsAreNotSentBeforeTheConfiguredLocalHour()
    {
        using var host = Host(new() { ["Notifications:SendAfterLocalHour"] = "9" });
        var (_, userId, email) = await SignInAsync(host, "Member");

        // 21:30 UTC is 03:00 the next day in India: too early.
        var night = DateTime.UtcNow.Date.AddHours(21).AddMinutes(30);
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12, now: night);

        var early = await ScanAsync(host, night, ignoreWindow: false);
        Assert.True(early.SkippedOutsideSendWindow);
        Assert.Empty(MailTo(email));

        // 04:30 UTC is 10:00 in India: allowed.
        var morning = night.AddHours(7);
        await ScanAsync(host, morning, ignoreWindow: false);
        Assert.Single(MailTo(email));
    }

    [Fact]
    public async Task DeactivatedMembers_AreNotEmailed()
    {
        using var host = Host();
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 12);
        await InDbAsync(host, db => db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false)));

        await ScanAsync(host);

        Assert.Empty(MailTo(email));
    }

    // ================================================================ availability

    private static async Task<HttpResponseMessage> BorrowAsync(HttpClient staff, int bookId, int memberId)
        => await staff.PostAsync($"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);

    private static async Task<int> TransactionIdAsync(HttpResponseMessage borrow)
    {
        borrow.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await borrow.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("transaction").GetProperty("transactionId").GetInt32();
    }

    [Fact]
    public async Task AMemberCanAskToBeToldAboutAnUnavailableBook_AndIsEmailedWhenItIsReturned()
    {
        using var host = Host();
        var (staff, _, _) = await SignInAsync(host, "Librarian");
        var (_, holderId, _) = await SignInAsync(host, "Member");
        var (waiter, _, waiterEmail) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host, total: 1);
        var title = await InDbAsync(host, db => db.Books.Where(b => b.BookId == bookId).Select(b => b.Title).SingleAsync());

        var loanId = await TransactionIdAsync(await BorrowAsync(staff, bookId, holderId)); // now unavailable

        var request = await waiter.PostAsJsonAsync("/api/availability-requests", new { bookId });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        Assert.Contains("emailed", await request.Content.ReadAsStringAsync());

        // Nothing yet: the book is still out.
        Assert.Empty(MailTo(waiterEmail));

        (await staff.PutAsync($"/api/borrowtransactions/return/{loanId}", null)).EnsureSuccessStatusCode();

        var mails = await WaitForMailAsync(waiterEmail, 1);
        var mail = Assert.Single(mails);
        Assert.Equal("Good News! Your Requested Book Is Available", mail.Subject);
        Assert.Contains(title, mail.Body);
        Assert.Contains("Available", mail.Body);

        var mine = await waiter.GetFromJsonAsync<JsonElement>("/api/availability-requests/mine");
        Assert.Equal("Notified", mine[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task RequestingAnAvailableBook_IsRefused()
    {
        using var host = Host();
        var (member, _, _) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host, total: 2);

        var response = await member.PostAsJsonAsync("/api/availability-requests", new { bookId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("available now", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnUnavailableBook_NeverTriggersANotification()
    {
        using var host = Host();
        var (_, holderId, _) = await SignInAsync(host, "Member");
        var (waiter, waiterId, waiterEmail) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host, total: 1, available: 0);
        await SeedLoanAsync(host, holderId, bookId, borrowedDaysAgo: 1);
        (await waiter.PostAsJsonAsync("/api/availability-requests", new { bookId })).EnsureSuccessStatusCode();

        int sent;
        using (var scope = host.Services.CreateScope())
        {
            var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
            sent = await availability.NotifyForBookAsync(bookId, CancellationToken.None);
            sent += await availability.NotifyAllWaitingAsync(CancellationToken.None);
        }

        Assert.Equal(0, sent);
        Assert.Empty(MailTo(waiterEmail));
        Assert.Equal(AvailabilityStatuses.Waiting,
            await InDbAsync(host, db => db.BookAvailabilityRequests.Where(r => r.UserId == waiterId).Select(r => r.Status).SingleAsync()));
    }

    [Fact]
    public async Task TheQueueIsFirstComeFirstServed_AndNeverOffersMoreCopiesThanAreFree()
    {
        using var host = Host();
        var (staff, _, _) = await SignInAsync(host, "Librarian");
        var (_, holder1, _) = await SignInAsync(host, "Member");
        var (_, holder2, _) = await SignInAsync(host, "Member");
        var (w1, _, mail1) = await SignInAsync(host, "Member");
        var (w2, _, mail2) = await SignInAsync(host, "Member");
        var (w3, _, mail3) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host, total: 2);

        var loan1 = await TransactionIdAsync(await BorrowAsync(staff, bookId, holder1));
        var loan2 = await TransactionIdAsync(await BorrowAsync(staff, bookId, holder2));

        foreach (var waiter in new[] { w1, w2, w3 })
        {
            (await waiter.PostAsJsonAsync("/api/availability-requests", new { bookId })).EnsureSuccessStatusCode();
            await Task.Delay(20); // distinct queue times
        }

        // One copy comes back: only the first in line hears about it.
        (await staff.PutAsync($"/api/borrowtransactions/return/{loan1}", null)).EnsureSuccessStatusCode();
        await WaitForMailAsync(mail1, 1);
        await Task.Delay(300);
        Assert.Single(MailTo(mail1));
        Assert.Empty(MailTo(mail2));
        Assert.Empty(MailTo(mail3));

        // A second copy comes back: the next in line, not the first again.
        (await staff.PutAsync($"/api/borrowtransactions/return/{loan2}", null)).EnsureSuccessStatusCode();
        await WaitForMailAsync(mail2, 1);
        await Task.Delay(300);
        Assert.Single(MailTo(mail1));
        Assert.Single(MailTo(mail2));
        Assert.Empty(MailTo(mail3));   // two copies are spoken for; the third waits
    }

    [Fact]
    public async Task ARequestIsCompleted_WhenTheMemberBorrowsTheBook()
    {
        using var host = Host();
        var (staff, _, _) = await SignInAsync(host, "Librarian");
        var (_, holderId, _) = await SignInAsync(host, "Member");
        var (waiter, waiterId, waiterEmail) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host, total: 1);
        var loanId = await TransactionIdAsync(await BorrowAsync(staff, bookId, holderId));
        (await waiter.PostAsJsonAsync("/api/availability-requests", new { bookId })).EnsureSuccessStatusCode();
        (await staff.PutAsync($"/api/borrowtransactions/return/{loanId}", null)).EnsureSuccessStatusCode();
        await WaitForMailAsync(waiterEmail, 1);

        (await BorrowAsync(waiter, bookId, waiterId)).EnsureSuccessStatusCode();

        Assert.Equal(AvailabilityStatuses.Fulfilled,
            await InDbAsync(host, db => db.BookAvailabilityRequests.Where(r => r.UserId == waiterId).Select(r => r.Status).SingleAsync()));
        Assert.Empty((await waiter.GetFromJsonAsync<JsonElement>("/api/availability-requests/mine")).EnumerateArray());
    }

    [Fact]
    public async Task RequestingTwiceKeepsOnePlaceInTheQueue_AndARequestCanBeCancelled()
    {
        using var host = Host();
        var (member, userId, _) = await SignInAsync(host, "Member");
        var bookId = await SeedBookAsync(host, total: 1, available: 0);

        var first = await member.PostAsJsonAsync("/api/availability-requests", new { bookId });
        var second = await member.PostAsJsonAsync("/api/availability-requests", new { bookId });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("already on the waiting list", await second.Content.ReadAsStringAsync());
        Assert.Equal(1, await InDbAsync(host, db => db.BookAvailabilityRequests.CountAsync(r => r.UserId == userId)));

        Assert.Equal(HttpStatusCode.NoContent, (await member.DeleteAsync($"/api/availability-requests/{bookId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.DeleteAsync($"/api/availability-requests/{bookId}")).StatusCode);
    }

    [Fact]
    public async Task AvailabilityRequests_AreForSignedInMembersOnly()
    {
        using var host = Host();
        var bookId = await SeedBookAsync(host, total: 1, available: 0);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await host.CreateClient().PostAsJsonAsync("/api/availability-requests", new { bookId })).StatusCode);

        var (admin, _, _) = await SignInAsync(host, "Admin");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync("/api/availability-requests", new { bookId })).StatusCode);

        var (member, _, _) = await SignInAsync(host, "Member");
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.PostAsJsonAsync("/api/availability-requests", new { bookId = 987654 })).StatusCode);
    }

    // ================================================================ admin monitoring

    [Fact]
    public async Task Admins_CanRunTheChecksAndSeeWhatWasSent()
    {
        using var host = Host();
        var (admin, _, _) = await SignInAsync(host, "Admin");
        var (_, userId, email) = await SignInAsync(host, "Member");
        await SeedLoanAsync(host, userId, await SeedBookAsync(host), borrowedDaysAgo: 17);

        var run = await admin.PostAsync("/api/notifications/run-now", null);
        run.EnsureSuccessStatusCode();
        Assert.Contains("overdueNotices", await run.Content.ReadAsStringAsync());
        Assert.Single(MailTo(email));

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/notifications?type=Overdue&status=Sent&pageSize=100");
        Assert.Contains(list.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("userEmail").GetString() == email);

        var summary = await admin.GetAsync("/api/notifications/summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Librarian")]
    public async Task NonAdmins_CannotUseTheMonitoringEndpoints(string role)
    {
        using var host = Host();
        var (client, _, _) = await SignInAsync(host, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/notifications/run-now", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/api/notifications")).StatusCode);
    }
}
