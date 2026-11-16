namespace LibraryManagement.Services;

/// <summary>Settings for forgot / reset password, section "PasswordReset".</summary>
public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>How long an emailed link works.</summary>
    public int TokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Absolute URL of the frontend page that lets the user choose a new
    /// password, for example https://your-site.netlify.app/reset-password.
    /// The email link is this URL plus ?token=...&amp;email=.... It comes only
    /// from configuration (never from request headers). When empty, no reset
    /// email is sent. In production set PasswordReset__FrontendResetUrl.
    /// </summary>
    public string FrontendResetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Minimum seconds between two reset emails to the same account, so the
    /// endpoint cannot be used to flood someone's inbox. 0 turns it off.
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Requests allowed per client IP address in each window (forgot + reset).</summary>
    public int RequestsPerWindow { get; set; } = 5;

    public int RateLimitWindowMinutes { get; set; } = 15;
}
