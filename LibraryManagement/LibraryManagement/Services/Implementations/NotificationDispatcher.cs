using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services.Implementations;

/// <summary>
/// The one place that sends a notification email. The guarantees:
/// <list type="bullet">
/// <item>A unique key is recorded (as Pending) BEFORE sending, so concurrent runs
///   or a restart cannot send the same notification twice.</item>
/// <item>It is marked Sent ONLY after <see cref="IEmailService"/> confirms.</item>
/// <item>A failure is recorded and retried after a delay, up to a maximum.</item>
/// <item>A worker that died mid-send leaves a stale Pending row, which is retried.</item>
/// </list>
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    // A Pending row older than this belongs to a worker that crashed.
    private static readonly TimeSpan StalePending = TimeSpan.FromMinutes(10);

    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ApplicationDbContext db,
        IEmailService email,
        IOptions<NotificationOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _email = email;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DispatchResult> SendAsync(
        NotificationDraft draft,
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var existing = await _db.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.DedupeKey == draft.DedupeKey, cancellationToken);

        int id;

        if (existing is null)
        {
            var row = new Notification
            {
                UserId = draft.UserId,
                BookId = draft.BookId,
                TransactionId = draft.TransactionId,
                NotificationType = draft.Type,
                DedupeKey = draft.DedupeKey,
                NotificationDate = draft.NotificationDate,
                Status = NotificationStatuses.Pending,
                Attempts = 1,
                CreatedAt = now,
                LastAttemptAt = now
            };

            _db.Notifications.Add(row);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The unique key already exists: another run claimed it first.
                _db.Entry(row).State = EntityState.Detached;
                return DispatchResult.Busy;
            }

            id = row.Id;
        }
        else
        {
            if (existing.Status == NotificationStatuses.Sent)
            {
                return DispatchResult.AlreadySent;
            }

            if (existing.Attempts >= _options.EffectiveMaxAttempts)
            {
                return DispatchResult.GaveUp;
            }

            var since = now - (existing.LastAttemptAt ?? existing.CreatedAt);
            var due = existing.Status == NotificationStatuses.Failed
                ? since >= TimeSpan.FromMinutes(Math.Max(0, _options.RetryDelayMinutes))
                : since >= StalePending; // Pending and old: the sender died

            if (!due)
            {
                return existing.Status == NotificationStatuses.Pending
                    ? DispatchResult.Busy
                    : DispatchResult.NotDue;
            }

            // Take over the retry. Only one of several competing workers sees
            // "1 row": the others find Attempts already moved on.
            var attempts = existing.Attempts;
            var claimed = await _db.Notifications
                .Where(n => n.Id == existing.Id && n.Attempts == attempts && n.Status != NotificationStatuses.Sent)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(n => n.Status, NotificationStatuses.Pending)
                    .SetProperty(n => n.Attempts, attempts + 1)
                    .SetProperty(n => n.LastAttemptAt, now), cancellationToken);

            if (claimed != 1)
            {
                return DispatchResult.Busy;
            }

            id = existing.Id;
        }

        try
        {
            await _email.SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            await MarkFailedAsync(id, $"{ex.Failure}: {ex.Message}");

            _logger.LogWarning(
                "Notification {NotificationId} ({Type}) failed: {Failure}. It will be retried.",
                id, draft.Type, ex.Failure);

            return DispatchResult.Failed;
        }
        catch (OperationCanceledException)
        {
            // Shutting down mid-send: the row stays Pending and is picked up again
            // once it is stale. Do not mark it Sent or Failed.
            throw;
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(id, $"Unexpected {ex.GetType().Name}");

            _logger.LogError(
                "Notification {NotificationId} ({Type}) failed unexpectedly ({ExceptionType}).",
                id, draft.Type, ex.GetType().Name);

            return DispatchResult.Failed;
        }

        // The email service confirmed: only now is it "Sent".
        var sentAt = DateTime.UtcNow;
        await _db.Notifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(n => n.Status, NotificationStatuses.Sent)
                .SetProperty(n => n.SentAt, sentAt)
                .SetProperty(n => n.ErrorMessage, (string?)null));

        _logger.LogInformation("Notification {NotificationId} ({Type}) sent.", id, draft.Type);

        return DispatchResult.Sent;
    }

    private async Task MarkFailedAsync(int id, string message)
    {
        var failedAt = DateTime.UtcNow;
        var text = message.Length > 300 ? message[..300] : message;

        // Not cancellable on purpose: the failure must be recorded even while stopping.
        await _db.Notifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(n => n.Status, NotificationStatuses.Failed)
                .SetProperty(n => n.LastAttemptAt, failedAt)
                .SetProperty(n => n.ErrorMessage, text));
    }
}
