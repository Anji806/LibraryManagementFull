using System.Net;
using System.Security.Cryptography;
using System.Text;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services.Implementations;

public class PasswordResetService : IPasswordResetService
{
    private const string InvalidMessage = "This password reset link is invalid or has expired.";
    private const string UsedMessage = "This password reset link has already been used.";

    private readonly ApplicationDbContext _db;
    private readonly PasswordResetOptions _options;
    private readonly ISessionService _sessions;
    private readonly ILoginHistoryService _loginHistory;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly PasswordHasher<User> _hasher = new(); // same hashing as login

    public PasswordResetService(
        ApplicationDbContext db,
        IOptions<PasswordResetOptions> options,
        ISessionService sessions,
        ILoginHistoryService loginHistory,
        IServiceScopeFactory scopes,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _options = options.Value;
        _sessions = sessions;
        _loginHistory = loginHistory;
        _scopes = scopes;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Forgot password
    // ------------------------------------------------------------------

    public async Task RequestResetAsync(string email)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Trim());

        // Unknown or inactive account: do nothing, and look exactly like success.
        if (user is null || !user.IsActive)
        {
            return;
        }

        if (!TryBuildLinkBase(out var linkBase))
        {
            _logger.LogWarning(
                "Password reset requested but PasswordReset:FrontendResetUrl is not a valid absolute URL; no email was sent.");
            return;
        }

        var now = DateTime.UtcNow;

        if (_options.ResendCooldownSeconds > 0)
        {
            var recent = now.AddSeconds(-_options.ResendCooldownSeconds);

            if (await _db.PasswordResetTokens.AnyAsync(t => t.UserId == user.UserId && t.CreatedAt > recent))
            {
                return; // a link was just sent; do not email again
            }
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                // Only the newest link works: retire every earlier one first.
                await _db.PasswordResetTokens
                    .Where(t => t.UserId == user.UserId && !t.IsUsed)
                    .ExecuteUpdateAsync(set => set.SetProperty(t => t.IsUsed, true));

                _db.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserId = user.UserId,
                    TokenHash = Hash(token),
                    CreatedAt = now,
                    ExpiryDate = now.AddMinutes(_options.TokenLifetimeMinutes)
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }
        catch (DbUpdateException)
        {
            // Two simultaneous requests for one account: the other one won.
            return;
        }

        var link = $"{linkBase}{(linkBase.Contains('?') ? '&' : '?')}" +
                   $"token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";

        SendInBackground(user.Email, user.FullName, link, _options.TokenLifetimeMinutes);
    }

    /// <summary>
    /// The email is sent off the request thread so that an existing account
    /// does not take measurably longer to answer than a missing one.
    /// Failures are logged (without the link) and never reach the caller.
    /// </summary>
    private void SendInBackground(string toEmail, string fullName, string link, int minutes)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

                await email.SendEmailAsync(
                    toEmail,
                    "Reset your Library Management password",
                    BuildEmailBody(fullName, link, minutes));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Password reset email could not be sent ({ExceptionType}).", ex.GetType().Name);
            }
        });
    }

    private static string BuildEmailBody(string fullName, string link, int minutes)
    {
        var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
        var href = WebUtility.HtmlEncode(link);

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;color:#2D3545">
              <h2 style="color:#011F5B;margin-bottom:4px">Reset your password</h2>
              <p>Hello {name},</p>
              <p>We received a request to reset the password for your Library Management account.
                 Click the button below to choose a new password.</p>
              <p style="margin:24px 0">
                <a href="{href}"
                   style="background:#011F5B;color:#ffffff;padding:12px 22px;border-radius:4px;text-decoration:none;font-weight:600">
                  Reset password
                </a>
              </p>
              <p>This link expires in <strong>{minutes} minutes</strong> and can be used only once.</p>
              <p style="font-size:13px;color:#595F6A">If the button does not work, copy this address into your browser:<br>
                 <span style="word-break:break-all">{href}</span></p>
              <hr style="border:none;border-top:1px solid #D9DBE0;margin:24px 0">
              <p style="font-size:13px;color:#595F6A">If you did not ask for this, you can ignore this email;
                 your password will not change.</p>
            </div>
            """;
    }

    // ------------------------------------------------------------------
    // Reset password
    // ------------------------------------------------------------------

    public async Task ResetPasswordAsync(string email, string token, string newPassword)
    {
        var hash = Hash(token);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim());

        var stored = user is null
            ? null
            : await _db.PasswordResetTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.TokenHash == hash);

        // The lookup above already matched on the hash; compare again in
        // constant time so the decision never depends on where strings differ.
        if (user is null || !user.IsActive || stored is null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored.TokenHash),
                Encoding.UTF8.GetBytes(hash)))
        {
            throw new ArgumentException(InvalidMessage);
        }

        if (stored.IsUsed)
        {
            throw new ArgumentException(UsedMessage);
        }

        var now = DateTime.UtcNow;

        if (stored.ExpiryDate <= now)
        {
            throw new ArgumentException(InvalidMessage);
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Claim the token with one conditional UPDATE: of several
            // simultaneous attempts with the same token exactly one gets 1 row.
            var claimed = await _db.PasswordResetTokens
                .Where(t => t.Id == stored.Id && !t.IsUsed && t.ExpiryDate > now)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(t => t.IsUsed, true)
                    .SetProperty(t => t.UsedAt, now));

            if (claimed != 1)
            {
                throw new ArgumentException(UsedMessage); // rolled back
            }

            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        });

        // The old password is gone, so end every signed-in session of the account.
        foreach (var sessionId in await _sessions.RevokeAllAsync(user.UserId))
        {
            try { await _loginHistory.RecordLogoutAsync(sessionId, DateTime.UtcNow); }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not write logout audit stored ({ExceptionType}).", ex.GetType().Name);
            }
        }
    }

    // ------------------------------------------------------------------

    private bool TryBuildLinkBase(out string linkBase)
    {
        linkBase = _options.FrontendResetUrl?.Trim() ?? string.Empty;

        return Uri.TryCreate(linkBase, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
