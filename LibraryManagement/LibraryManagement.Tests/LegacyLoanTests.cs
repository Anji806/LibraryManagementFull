using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Tests;

/// <summary>Loans that existed before due dates and fines were introduced.</summary>
public class LegacyLoanTests : IClassFixture<LibraryApiFactory>
{
    private readonly LibraryApiFactory _factory;

    // SQLite returns timestamps without a kind; the app treats them as UTC.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public LegacyLoanTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    private static DateTime Utc(int month, int day, int hour = 12)
        => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static BorrowingPolicy PolicyWithFinesFrom(DateOnly start)
        => new(Options.Create(new BorrowingOptions { FinesStartDate = start }));

    // Borrowed 1 Nov -> due 15 Nov. Fines go live on 20 Nov.
    private static readonly DateTime Borrowed = Utc(11, 1, 10);
    private static readonly DateOnly GoLive = new(2026, 11, 20);

    [Fact]
    public void AlreadyOverdueLoan_IsShownOverdue_ButIsNotFinedForDaysBeforeGoLive()
    {
        var policy = PolicyWithFinesFrom(GoLive);
        var loan = new BorrowTransaction { BorrowDate = Borrowed, DueDate = policy.ComputeDueDate(Borrowed) };

        // Go-live day: 5 days overdue, nothing charged yet.
        var onGoLive = policy.Evaluate(loan, Utc(11, 20));
        Assert.True(onGoLive.IsOverdue);
        Assert.Equal(5, onGoLive.OverdueDays);
        Assert.Equal("Overdue", onGoLive.DisplayStatus);
        Assert.Equal(0m, onGoLive.Fine);

        // The fine only grows from the days after go-live.
        Assert.Equal(5m, policy.Evaluate(loan, Utc(11, 21)).Fine);
        Assert.Equal(10m, policy.Evaluate(loan, Utc(11, 22)).Fine);
    }

    [Fact]
    public void ReturningAnAlreadyOverdueLoan_IsFinedOnlyFromGoLive()
    {
        var policy = PolicyWithFinesFrom(GoLive);
        var due = policy.ComputeDueDate(Borrowed);

        Assert.Equal(0m, policy.CalculateFine(due, Utc(11, 19)));   // before go-live
        Assert.Equal(0m, policy.CalculateFine(due, Utc(11, 20)));   // on go-live day
        Assert.Equal(15m, policy.CalculateFine(due, Utc(11, 23)));  // 3 days after
    }

    [Fact]
    public void LoanDueAfterGoLive_IsFinedFromItsOwnDueDate()
    {
        var policy = PolicyWithFinesFrom(GoLive);
        var borrowedLater = Utc(11, 15, 10);                         // due 29 Nov
        var due = policy.ComputeDueDate(borrowedLater);

        Assert.Equal(0m, policy.CalculateFine(due, Utc(11, 29)));
        Assert.Equal(20m, policy.CalculateFine(due, Utc(12, 3)));    // 4 days late
    }

    [Fact]
    public void WithoutAGoLiveDate_FinesCountFromTheDueDate()
    {
        var policy = new BorrowingPolicy(Options.Create(new BorrowingOptions()));
        var due = policy.ComputeDueDate(Borrowed);

        Assert.Equal(20m, policy.CalculateFine(due, Utc(11, 19)));
    }

    [Fact]
    public async Task Backfill_GivesExistingLoansTheSameDueDateNewLoansGet_AndChangesNothingElse()
    {
        var policy = _factory.Services.GetRequiredService<BorrowingPolicy>();
        var borrowedActive = DateTime.UtcNow.AddDays(-40);
        var borrowedReturned = DateTime.UtcNow.AddDays(-60);
        var returnedAt = DateTime.UtcNow.AddDays(-10);   // would have been very late

        int activeId, returnedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var suffix = Guid.NewGuid().ToString("N");
            var book = new Book
            {
                Title = "Legacy " + suffix, ISBN = suffix[..13], TotalCopies = 2, AvailableCopies = 1, PublishedYear = 2000,
                Author = new Author { AuthorName = "A" }, Category = new Category { CategoryName = "C" }
            };
            var active = new BorrowTransaction { Book = book, BorrowDate = borrowedActive, Status = "Borrowed" };
            var returned = new BorrowTransaction
            {
                Book = book, BorrowDate = borrowedReturned, ReturnDate = returnedAt, Status = "Returned"
            };
            db.BorrowTransactions.AddRange(active, returned);
            await db.SaveChangesAsync();
            activeId = active.TransactionId;
            returnedId = returned.TransactionId;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await BorrowDueDateBackfill.RunAsync(db, policy) >= 2);
            Assert.Equal(0, await BorrowDueDateBackfill.RunAsync(db, policy)); // idempotent
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var active = await db.BorrowTransactions.AsNoTracking().SingleAsync(t => t.TransactionId == activeId);
            var returned = await db.BorrowTransactions.AsNoTracking().SingleAsync(t => t.TransactionId == returnedId);

            Assert.Equal(policy.ComputeDueDate(active.BorrowDate), AsUtc(active.DueDate!.Value));
            Assert.Equal(policy.ComputeDueDate(returned.BorrowDate), AsUtc(returned.DueDate!.Value));

            // Nothing else changed, and no fine was created for past loans.
            Assert.Equal("Borrowed", active.Status);
            Assert.Null(active.ReturnDate);
            Assert.Equal("Returned", returned.Status);
            Assert.Equal(0m, returned.FineAmount);
            Assert.Equal(0m, returned.FinePaid);
            Assert.Equal(0m, active.FineAmount);

            // A returned loan is not retroactively marked as owing.
            policy.Apply(returned, DateTime.UtcNow);
            Assert.Equal("Returned", returned.DisplayStatus);
            Assert.Equal(0m, returned.OutstandingFine);
        }
    }

    [Fact]
    public async Task Backfill_LeavesLoansThatAlreadyHaveADueDateAlone()
    {
        var policy = _factory.Services.GetRequiredService<BorrowingPolicy>();
        var custom = DateTime.UtcNow.AddDays(30);

        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var suffix = Guid.NewGuid().ToString("N");
            var loan = new BorrowTransaction
            {
                BorrowDate = DateTime.UtcNow, DueDate = custom, Status = "Borrowed",
                Book = new Book
                {
                    Title = "Has due " + suffix, ISBN = suffix[..13], TotalCopies = 1, AvailableCopies = 0, PublishedYear = 2000,
                    Author = new Author { AuthorName = "A" }, Category = new Category { CategoryName = "C" }
                }
            };
            db.BorrowTransactions.Add(loan);
            await db.SaveChangesAsync();
            id = loan.TransactionId;

            await BorrowDueDateBackfill.RunAsync(db, policy);
        }

        using var check = _factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .BorrowTransactions.AsNoTracking().SingleAsync(t => t.TransactionId == id);
        Assert.True(Math.Abs((AsUtc(saved.DueDate!.Value) - custom).TotalSeconds) < 1);
    }
}
