using LibraryManagement.Models;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services;

/// <summary>Borrowing rules; override in configuration section "Borrowing".</summary>
public class BorrowingOptions
{
    public const string SectionName = "Borrowing";

    /// <summary>How long a member may keep a book.</summary>
    public int LoanPeriodDays { get; set; } = 14;

    /// <summary>Fine per overdue day, in rupees.</summary>
    public decimal FinePerDay { get; set; } = 5m;

    /// <summary>A loan is "Due Soon" when this many days (or fewer) remain.</summary>
    public int DueSoonDays { get; set; } = 2;

    /// <summary>
    /// IANA zone that defines when a "day" ends (a book returned any time on
    /// the due date is on time).
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}

/// <summary>What the rules say about one loan at one moment.</summary>
public readonly record struct BorrowingStatus(
    bool IsOverdue,
    int OverdueDays,
    int? DaysRemaining,
    decimal Fine,
    string DisplayStatus);

/// <summary>
/// The single place that decides due dates, overdue days and fines.
/// All timestamps are stored in UTC; "days" are calendar days in the
/// configured library time zone, so the due date is the end of that day.
/// Money is <see cref="decimal"/> (never floating point).
/// </summary>
public class BorrowingPolicy
{
    private readonly BorrowingOptions _options;
    private readonly TimeZoneInfo _zone;

    public BorrowingPolicy(IOptions<BorrowingOptions> options)
    {
        _options = options.Value;

        if (_options.LoanPeriodDays < 1)
            throw new InvalidOperationException("Borrowing:LoanPeriodDays must be at least 1.");
        if (_options.FinePerDay < 0)
            throw new InvalidOperationException("Borrowing:FinePerDay cannot be negative.");

        _zone = ResolveZone(_options.TimeZoneId);
    }

    public decimal FinePerDay => _options.FinePerDay;

    public int LoanPeriodDays => _options.LoanPeriodDays;

    /// <summary>End of the due day (borrow day + loan period), as a UTC instant.</summary>
    public DateTime ComputeDueDate(DateTime borrowedUtc)
    {
        var dueDay = LocalDate(borrowedUtc).AddDays(_options.LoanPeriodDays);
        var endOfDay = dueDay.ToDateTime(TimeOnly.MaxValue);

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(endOfDay, DateTimeKind.Unspecified), _zone);
    }

    /// <summary>Whole calendar days after the due day (0 when on time).</summary>
    public int OverdueDays(DateTime dueUtc, DateTime endUtc)
        => Math.Max(0, LocalDate(endUtc).DayNumber - LocalDate(dueUtc).DayNumber);

    public decimal CalculateFine(DateTime dueUtc, DateTime endUtc)
        => OverdueDays(dueUtc, endUtc) * _options.FinePerDay;

    /// <summary>
    /// Evaluates a loan. A returned loan uses its stored return date and fine;
    /// an active loan is measured against <paramref name="nowUtc"/>. Loans from
    /// before due dates existed (DueDate is null) are never overdue and owe nothing.
    /// </summary>
    public BorrowingStatus Evaluate(BorrowTransaction t, DateTime nowUtc)
    {
        if (t.ReturnDate is { } returned)
        {
            var fine = t.FineAmount;
            var late = t.DueDate is { } due ? OverdueDays(due, returned) : 0;

            return new BorrowingStatus(
                false, late, null, fine,
                fine - t.FinePaid > 0 ? "Fine Pending" : "Returned");
        }

        if (t.DueDate is not { } dueDate)
        {
            return new BorrowingStatus(false, 0, null, 0m, "Borrowed");
        }

        var overdue = OverdueDays(dueDate, nowUtc);

        if (overdue > 0)
        {
            return new BorrowingStatus(
                true, overdue, null, overdue * _options.FinePerDay, "Overdue");
        }

        var remaining = LocalDate(dueDate).DayNumber - LocalDate(nowUtc).DayNumber;

        return new BorrowingStatus(
            false, 0, remaining, 0m,
            remaining <= _options.DueSoonDays ? "Due Soon" : "Borrowed");
    }

    /// <summary>Fills the calculated (non-stored) fields used by API responses.</summary>
    public void Apply(BorrowTransaction t, DateTime nowUtc)
    {
        var status = Evaluate(t, nowUtc);

        t.IsOverdue = status.IsOverdue;
        t.OverdueDays = status.OverdueDays;
        t.DaysRemaining = status.DaysRemaining;
        t.CurrentFine = status.Fine;
        t.OutstandingFine = Math.Max(0m, status.Fine - t.FinePaid);
        t.DisplayStatus = status.DisplayStatus;
    }

    private DateOnly LocalDate(DateTime utc)
    {
        var instant = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instant, _zone));
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Slim containers may lack tzdata; India has no daylight saving.
            if (id is "Asia/Kolkata" or "India Standard Time")
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "IST", TimeSpan.FromMinutes(330), "IST", "IST");
            }

            throw new InvalidOperationException(
                $"Borrowing:TimeZoneId '{id}' is not a known time zone.", ex);
        }
    }
}
