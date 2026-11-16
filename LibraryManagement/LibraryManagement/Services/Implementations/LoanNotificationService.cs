using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services.Implementations;

/// <summary>Due-soon reminders and overdue notices for loans that are still out.</summary>
public class LoanNotificationService : ILoanNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;
    private readonly BorrowingPolicy _policy;
    private readonly NotificationOptions _options;
    private readonly EmailSettings _email;
    private readonly ILogger<LoanNotificationService> _logger;

    public LoanNotificationService(
        ApplicationDbContext db,
        INotificationDispatcher dispatcher,
        BorrowingPolicy policy,
        IOptions<NotificationOptions> options,
        IOptions<EmailSettings> email,
        ILogger<LoanNotificationService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _policy = policy;
        _options = options.Value;
        _email = email.Value;
        _logger = logger;
    }

    public async Task<LoanScanSummary> RunAsync(
        DateTime? nowUtc,
        bool ignoreSendWindow,
        CancellationToken cancellationToken)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(_email.SenderEmail) || string.IsNullOrWhiteSpace(_email.Password))
        {
            _logger.LogWarning("Loan notifications skipped: email is not configured.");
            return new LoanScanSummary(0, 0, 0, 0, false, true);
        }

        if (!ignoreSendWindow && _policy.ToLocalTime(now).Hour < _options.SendAfterLocalHour)
        {
            return new LoanScanSummary(0, 0, 0, 0, true, false);
        }

        var today = _policy.ToLocalDate(now);
        var dueDays = _options.EffectiveDueReminderDays;
        var every = _options.EffectiveOverdueEveryDays;

        // Only loans that are still out and have a member to write to.
        var loans = await _db.BorrowTransactions
            .AsNoTracking()
            .Include(t => t.Member)
            .Include(t => t.Book)
            .Where(t => t.ReturnDate == null && t.DueDate != null && t.MemberId != null)
            .ToListAsync(cancellationToken);

        int dueSent = 0, overdueSent = 0, failed = 0, skipped = 0;

        foreach (var loan in loans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var member = loan.Member;
            if (member is null || !member.IsActive || string.IsNullOrWhiteSpace(member.Email))
            {
                continue;
            }

            var due = _policy.ToLocalDate(loan.DueDate!.Value);
            var daysLeft = due.DayNumber - today.DayNumber;

            NotificationDraft draft;
            (string Subject, string Body) mail;
            var type = string.Empty;

            if (daysLeft >= 0 && dueDays.Contains(daysLeft))
            {
                type = NotificationTypes.DueReminder;
                draft = new NotificationDraft(
                    member.UserId, loan.BookId, loan.TransactionId, type,
                    $"due:{loan.TransactionId}:{daysLeft}", today);
                mail = NotificationEmails.DueSoon(
                    member.FullName, loan.Book.Title,
                    _policy.ToLocalDate(loan.BorrowDate), due, daysLeft, _options);
            }
            else if (daysLeft < 0 && (-daysLeft - 1) % every == 0)
            {
                // The fine comes from the existing rules, never recalculated here.
                var status = _policy.Evaluate(loan, now);
                var outstanding = Math.Max(0m, status.Fine - loan.FinePaid);

                type = NotificationTypes.Overdue;
                draft = new NotificationDraft(
                    member.UserId, loan.BookId, loan.TransactionId, type,
                    $"overdue:{loan.TransactionId}:{today:yyyyMMdd}", today);
                mail = NotificationEmails.Overdue(
                    member.FullName, loan.Book.Title, due, status.OverdueDays,
                    outstanding, _policy.FinePerDay, _options);
            }
            else
            {
                continue;
            }

            var result = await _dispatcher.SendAsync(
                draft, member.Email, mail.Subject, mail.Body, cancellationToken);

            switch (result)
            {
                case DispatchResult.Sent when type == NotificationTypes.DueReminder: dueSent++; break;
                case DispatchResult.Sent: overdueSent++; break;
                case DispatchResult.Failed: failed++; break;
                default: skipped++; break;
            }
        }

        if (dueSent + overdueSent + failed > 0)
        {
            _logger.LogInformation(
                "Loan notifications: {Due} due reminder(s), {Overdue} overdue notice(s), {Failed} failed.",
                dueSent, overdueSent, failed);
        }

        return new LoanScanSummary(dueSent, overdueSent, failed, skipped, false, false);
    }
}
