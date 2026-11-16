namespace LibraryManagement.Services;

/// <summary>
/// Gmail SMTP settings, bound from the "EmailSettings" configuration section.
/// SmtpServer, Port and the other options are not secret and live in appsettings.json.
/// <see cref="SenderEmail"/> and <see cref="Password"/> (the Gmail App Password)
/// come from User Secrets or environment variables, never from source control:
/// <code>
/// dotnet user-secrets set "EmailSettings:SenderEmail" "you@gmail.com"
/// dotnet user-secrets set "EmailSettings:Password" "xxxx xxxx xxxx xxxx"
/// </code>
/// On Render use the variables EmailSettings__SenderEmail and EmailSettings__Password.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpServer { get; set; } = "smtp.gmail.com";

    /// <summary>587 with STARTTLS (see <see cref="EnableSsl"/>).</summary>
    public int Port { get; set; } = 587;

    /// <summary>The Gmail account: used to sign in and as the visible sender.</summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>The 16-character Gmail App Password. Secret-store / environment only.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// With SmtpClient and port 587 this negotiates STARTTLS: the connection
    /// starts in plain text and is upgraded to TLS before any credentials are sent.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Display name shown next to the sender address.</summary>
    public string SenderName { get; set; } = "Greenleaf Library";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Turns on POST /api/email/test (admins only). Off by default so it cannot
    /// be used in production; set to true in appsettings.Development.json.
    /// </summary>
    public bool EnableTestEndpoint { get; set; }
}
