using System.Net.Mail;
using LibraryManagement.DTOs.Email;
using LibraryManagement.Services;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Controllers;

/// <summary>
/// Temporary diagnostic endpoint to verify the Gmail SMTP setup. Administrators
/// only, and it answers 404 unless EmailSettings:EnableTestEndpoint is true (set in
/// appsettings.Development.json, off everywhere else).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _email;
    private readonly EmailSettings _settings;

    public EmailController(IEmailService email, IOptions<EmailSettings> settings)
    {
        _email = email;
        _settings = settings.Value;
    }

    // POST: api/email/test
    [HttpPost("test")]
    public async Task<IActionResult> SendTest(TestEmailDto dto, CancellationToken cancellationToken)
    {
        if (!_settings.EnableTestEndpoint)
        {
            return NotFound();
        }

        // [EmailAddress] is lenient ("a b@c.test" passes); also require a plain,
        // parseable address with no display-name part.
        if (!MailAddress.TryCreate(dto.ToEmail, out var address) ||
            !string.Equals(address.Address, dto.ToEmail, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Enter a valid recipient email address." });
        }

        try
        {
            await _email.SendEmailAsync(
                dto.ToEmail,
                "Library Management – test email",
                """
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px">
                  <h2 style="color:#011F5B">Gmail SMTP is working</h2>
                  <p>This is a test message from the Library Management API.</p>
                  <p style="color:#595F6A;font-size:13px">If you did not expect this email you can ignore it.</p>
                </div>
                """,
                cancellationToken);

            return Ok(new { message = $"Test email sent to {dto.ToEmail}." });
        }
        catch (EmailDeliveryException ex)
        {
            // The message is written to be safe to show: no credentials in it.
            var status = ex.Failure switch
            {
                EmailFailure.InvalidRecipient => StatusCodes.Status400BadRequest,
                EmailFailure.AuthenticationFailed => StatusCodes.Status502BadGateway,
                EmailFailure.ConnectionFailed => StatusCodes.Status503ServiceUnavailable,
                EmailFailure.NotConfigured => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };

            return StatusCode(status, new { failure = ex.Failure.ToString(), message = ex.Message });
        }
    }
}
