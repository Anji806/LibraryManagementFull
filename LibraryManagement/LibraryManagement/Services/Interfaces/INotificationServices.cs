using LibraryManagement.Models;

namespace LibraryManagement.Services.Interfaces;

public enum DispatchResult
{
    /// <summary>The email service accepted the message; recorded as Sent.</summary>
    Sent,

    /// <summary>Already sent earlier (or by another worker): nothing to do.</summary>
    AlreadySent,

    /// <summary>Sending failed; recorded as Failed and will be retried later.</summary>
    Failed,

    /// <summary>Failed on every allowed attempt; no more retries.</summary>
    GaveUp,

    /// <summary>A failed attempt exists but its retry delay has not passed yet.</summary>
    NotDue,

    /// <summary>Another worker is sending this exact notification right now.</summary>
    Busy
}

/// <param name="Type">One of <see cref="NotificationTypes"/>.</param>
/// <param name="DedupeKey">Unique identity of the notification (see <see cref="Notification.DedupeKey"/>).</param>
public sealed record NotificationDraft(
    int UserId,
    int BookId,
    int? TransactionId,
    string Type,
    string DedupeKey,
    DateOnly NotificationDate);

public interface INotificationDispatcher
{
    /// <summary>
    /// Sends one notification exactly once: claims its unique key first, sends
    /// through <see cref="IEmailService"/>, and marks it Sent only after the
    /// email service confirms. Failures are recorded and retried safely.
    /// </summary>
    Task<DispatchResult> SendAsync(
        NotificationDraft draft,
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken);
}

public sealed record LoanScanSummary(
    int DueReminders,
    int OverdueNotices,
    int Failed,
    int SkippedAlreadyHandled,
    bool SkippedOutsideSendWindow,
    bool SkippedEmailNotConfigured);

public interface ILoanNotificationService
{
    /// <summary>
    /// Finds unreturned loans that are due soon or overdue and sends the
    /// matching notifications. Safe to run repeatedly and concurrently.
    /// </summary>
    /// <param name="nowUtc">Override for tests; defaults to the current time.</param>
    /// <param name="ignoreSendWindow">Skip the "not before 09:00 local time" rule.</param>
    Task<LoanScanSummary> RunAsync(
        DateTime? nowUtc,
        bool ignoreSendWindow,
        CancellationToken cancellationToken);
}

public sealed record AvailabilityRequestResult(int BookId, string Status, int Position, bool AlreadyWaiting);

public sealed record MyAvailabilityRequest(int BookId, string Title, string Status, DateTime CreatedAt, int? Position);

public interface IAvailabilityService
{
    /// <summary>Puts the member in the queue for an unavailable book.</summary>
    Task<AvailabilityRequestResult> RequestAsync(int userId, int bookId);

    Task<bool> CancelAsync(int userId, int bookId);

    Task<List<MyAvailabilityRequest>> GetMineAsync(int userId);

    /// <summary>
    /// Emails the next people in the queue, first come first served, but never
    /// more of them than there are free copies. Returns how many were emailed.
    /// </summary>
    Task<int> NotifyForBookAsync(int bookId, CancellationToken cancellationToken);

    /// <summary>Runs <see cref="NotifyForBookAsync"/> for every book that has waiters and a free copy.</summary>
    Task<int> NotifyAllWaitingAsync(CancellationToken cancellationToken);

    /// <summary>Starts <see cref="NotifyForBookAsync"/> without making the caller wait for the emails.</summary>
    void NotifyForBookInBackground(int bookId);

    /// <summary>The member borrowed the book, so their request is done.</summary>
    Task MarkFulfilledAsync(int userId, int bookId);
}
