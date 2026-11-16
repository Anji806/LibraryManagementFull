namespace LibraryManagement.Services.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends one HTML email. Throws <see cref="EmailDeliveryException"/> with a
    /// <see cref="EmailFailure"/> category when it cannot be delivered; the
    /// exception message is safe to show to an administrator and never contains
    /// credentials.
    /// </summary>
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

public enum EmailFailure
{
    /// <summary>Username, password or sender address is not configured.</summary>
    NotConfigured,

    /// <summary>The recipient address is malformed or was refused by the server.</summary>
    InvalidRecipient,

    /// <summary>Gmail rejected the username / App Password.</summary>
    AuthenticationFailed,

    /// <summary>The SMTP server could not be reached, or timed out.</summary>
    ConnectionFailed,

    Unknown
}

public class EmailDeliveryException : Exception
{
    public EmailFailure Failure { get; }

    public EmailDeliveryException(EmailFailure failure, string message, Exception? inner = null)
        : base(message, inner)
    {
        Failure = failure;
    }
}
