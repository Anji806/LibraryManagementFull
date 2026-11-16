namespace LibraryManagement.Services;

/// <summary>Email notification settings, section "Notifications".</summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Master switch for the background scheduler.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the scheduler looks for notifications to send.</summary>
    public int ScanIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Due and overdue emails are only sent from this hour (library time zone)
    /// onwards, so members are not emailed in the middle of the night. 0 = any time.
    /// </summary>
    public int SendAfterLocalHour { get; set; } = 9;

    /// <summary>
    /// Send a reminder when this many days remain before the due date.
    /// Default (when not configured): 2 and 1.
    /// </summary>
    public int[]? DueReminderDaysBefore { get; set; }

    /// <summary>Overdue notice frequency in days (1 = every day).</summary>
    public int OverdueEveryDays { get; set; } = 1;

    /// <summary>Delivery attempts per notification before giving up.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Wait before a failed email is tried again.</summary>
    public int RetryDelayMinutes { get; set; } = 30;

    /// <summary>
    /// A member who was told a copy is free keeps "holding" that copy for this
    /// long, so the next returned copy goes to the next person in the queue
    /// instead of the same copy being offered to everyone.
    /// </summary>
    public int AvailabilityHoldHours { get; set; } = 24;

    /// <summary>Shown in emails (for example "library@school.org, 044-1234567"). Optional.</summary>
    public string LibraryContact { get; set; } = string.Empty;

    /// <summary>Frontend address used for links in emails, for example https://site.netlify.app. Optional.</summary>
    public string AppUrl { get; set; } = string.Empty;

    public IReadOnlyList<int> EffectiveDueReminderDays
        => DueReminderDaysBefore is { Length: > 0 } days
            ? days.Where(d => d >= 0).Distinct().ToArray()
            : [2, 1];

    public int EffectiveOverdueEveryDays => Math.Max(1, OverdueEveryDays);

    public int EffectiveMaxAttempts => Math.Max(1, MaxAttempts);
}
