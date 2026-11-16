using System.Net;
using System.Net.Mail;
using LibraryManagement.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services.Implementations;

/// <summary>
/// Sends mail through Gmail SMTP with System.Net.Mail. A new SmtpClient is
/// created and disposed for every message: SmtpClient is not safe to share
/// between concurrent requests.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                // Line breaks in a subject would allow header injection.
                Subject = (subject ?? string.Empty).ReplaceLineEndings(" ").Trim(),
                Body = body ?? string.Empty,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient(_settings.SmtpServer, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl, // STARTTLS on port 587
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password),
                Timeout = Math.Max(1, _settings.TimeoutSeconds) * 1000
            };

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email \"{Subject}\" sent to {Recipient}.", message.Subject, Mask(toEmail));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller gave up; not a delivery failure
        }
        catch (Exception ex)
        {
            var failure = Translate(ex);

            // The log gets the category and the exception type only: the
            // exception text from the SMTP layer can echo the server's reply.
            _logger.LogWarning(
                "Email to {Recipient} failed: {Failure} ({ExceptionType}).",
                Mask(toEmail), failure.Failure, ex.GetType().Name);

            throw failure;
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new EmailDeliveryException(
                EmailFailure.NotConfigured,
                "Email is not configured. Set EmailSettings:SenderEmail and EmailSettings:Password " +
                "(user secrets, or the EmailSettings__SenderEmail / EmailSettings__Password environment variables).");
        }
    }

    /// <summary>Turns a low-level mail/network exception into a safe, categorised one.</summary>
    public static EmailDeliveryException Translate(Exception ex)
    {
        switch (ex)
        {
            case EmailDeliveryException already:
                return already;

            // new MailAddress("not-an-email") and friends.
            case FormatException or ArgumentException:
                return new EmailDeliveryException(
                    EmailFailure.InvalidRecipient,
                    "The recipient email address is not valid.", ex);

            case SmtpFailedRecipientException:
                return new EmailDeliveryException(
                    EmailFailure.InvalidRecipient,
                    "The mail server refused the recipient address.", ex);

            case SmtpException smtp when IsAuthenticationFailure(smtp):
                return new EmailDeliveryException(
                    EmailFailure.AuthenticationFailed,
                    "Gmail rejected the sign-in. Check EmailSettings:SenderEmail and that EmailSettings:Password " +
                    "is a current App Password (not the normal Gmail password).", ex);

            case SmtpException smtp when IsConnectionFailure(smtp):
            case TimeoutException:
            case OperationCanceledException: // HttpClient-style timeout without our token
            case System.Net.Sockets.SocketException:
            case IOException:
                return new EmailDeliveryException(
                    EmailFailure.ConnectionFailed,
                    "Could not reach the mail server. Check the network, " +
                    "firewall and that outbound SMTP is allowed.", ex);

            default:
                return new EmailDeliveryException(
                    EmailFailure.Unknown, "The email could not be sent.", ex);
        }
    }

    private static bool IsAuthenticationFailure(SmtpException ex)
        => ex.StatusCode is SmtpStatusCode.ClientNotPermitted or SmtpStatusCode.MustIssueStartTlsFirst
           || Contains(ex, "5.7.8") || Contains(ex, "5.7.0") || Contains(ex, "5.7.9")
           || Contains(ex, "Username and Password not accepted")
           || Contains(ex, "Authentication Required")
           || Contains(ex, "authentication");

    private static bool IsConnectionFailure(SmtpException ex)
        => ex.InnerException is System.Net.Sockets.SocketException or IOException or TimeoutException
           || (ex.StatusCode is SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.GeneralFailure
               && ex.InnerException is not null)
           || Contains(ex, "Unable to connect")
           || Contains(ex, "timed out")
           || Contains(ex, "connection");

    private static bool Contains(Exception ex, string text)
        => ex.Message.Contains(text, StringComparison.OrdinalIgnoreCase);

    // a***@gmail.com: enough to correlate in logs without storing the full address.
    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : email[0] + "***" + email[at..];
    }
}
