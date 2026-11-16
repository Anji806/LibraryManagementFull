namespace LibraryManagement.Models;

public static class NotificationTypes
{
    public const string DueReminder = "DueReminder";
    public const string Overdue = "Overdue";
    public const string BookAvailable = "BookAvailable";
}

public static class NotificationStatuses
{
    /// <summary>Claimed by a worker; the email has not been confirmed yet.</summary>
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

/// <summary>
/// One email the system decided to send, and what happened to it.
/// <see cref="DedupeKey"/> is unique, so the same reminder can never be
/// recorded (and therefore sent) twice, however many times the scheduler runs,
/// overlaps with itself or restarts.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookId { get; set; }

    /// <summary>The loan (BorrowTransaction) this is about, for loan reminders.</summary>
    public int? TransactionId { get; set; }

    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// What makes this email unique, for example "due:42:2" (loan 42, two days
    /// before the due date) or "overdue:42:20261120" (loan 42, that day).
    /// </summary>
    public string DedupeKey { get; set; } = string.Empty;

    /// <summary>The library-local calendar day the notification belongs to.</summary>
    public DateOnly NotificationDate { get; set; }

    public string Status { get; set; } = NotificationStatuses.Pending;

    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    /// <summary>Set only after the email service confirmed delivery to the mail server.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Short, safe failure reason (never credentials).</summary>
    public string? ErrorMessage { get; set; }
}

public static class AvailabilityStatuses
{
    public const string Waiting = "Waiting";
    /// <summary>The member was emailed that a copy is free.</summary>
    public const string Notified = "Notified";
    /// <summary>The member borrowed the book.</summary>
    public const string Fulfilled = "Fulfilled";
    public const string Cancelled = "Cancelled";
    /// <summary>The email could not be delivered after all retries.</summary>
    public const string Failed = "Failed";
}

/// <summary>A member waiting to be told that an unavailable book can be borrowed.</summary>
public class BookAvailabilityRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BookId { get; set; }

    public string Status { get; set; } = AvailabilityStatuses.Waiting;

    /// <summary>First come, first served: the queue is ordered by this.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime? NotifiedAt { get; set; }
}
