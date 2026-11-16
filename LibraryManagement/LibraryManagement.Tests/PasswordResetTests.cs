using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using LibraryManagement.Data;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LibraryManagement.Tests;

/// <summary>Forgot password / reset password, end to end through the real API.</summary>
public class PasswordResetTests : IClassFixture<LibraryApiFactory>
{
    private const string OldPassword = "Test@12345";
    private const string NewPassword = "Brand-New#2026";

    /// <summary>Stands in for Gmail: records what would have been sent.</summary>
    private sealed class FakeEmail : IEmailService
    {
        public static readonly List<(string To, string Subject, string Body)> Sent = new();

        public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            lock (Sent) Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private readonly WebApplicationFactory<Program> _factory;

    public PasswordResetTests(LibraryApiFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, FakeEmail>();
        }));
    }

    private sealed record Auth(string Token);

    // ---------------------------------------------------------------- helpers

    private async Task<string> NewUserAsync(string role = "Member", bool active = true)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";

        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = $"{role} Person", email, password = OldPassword, deviceId = "seed" }))
            .EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        user.IsActive = active;
        await db.SaveChangesAsync();
        return email;
    }

    private Task<HttpResponseMessage> ForgotAsync(string email)
        => _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });

    private Task<HttpResponseMessage> ResetAsync(
        string email, string token, string password = NewPassword, string? confirm = null)
        => _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new
        {
            email,
            token,
            newPassword = password,
            confirmPassword = confirm ?? password
        });

    private Task<HttpResponseMessage> LoginAsync(string email, string password, string device = "d1")
        => _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password, deviceId = device });

    /// <summary>Waits for the (background) email and returns the raw token from its link.</summary>
    private static async Task<string> TokenFromEmailAsync(string to, int skip = 0)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            lock (FakeEmail.Sent)
            {
                var mails = FakeEmail.Sent.Where(m => m.To == to).ToList();
                if (mails.Count > skip)
                {
                    var match = Regex.Match(mails[skip].Body, @"[?&]token=([^&""'<\s]+)");
                    Assert.True(match.Success, "The email must contain the reset link.");
                    return Uri.UnescapeDataString(match.Groups[1].Value);
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No reset email arrived for {to}.");
    }

    private static int MailCount(string to)
    {
        lock (FakeEmail.Sent) return FakeEmail.Sent.Count(m => m.To == to);
    }

    private async Task<string> RequestTokenAsync(string email, int skip = 0)
    {
        (await ForgotAsync(email)).EnsureSuccessStatusCode();
        return await TokenFromEmailAsync(email, skip);
    }

    // ---------------------------------------------------------------- forgot

    [Theory]
    [InlineData("Admin")]
    [InlineData("Member")]
    [InlineData("Librarian")]
    public async Task Forgot_ForARegisteredAccount_EmailsAResetLink(string role)
    {
        var email = await NewUserAsync(role);

        var response = await ForgotAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await TokenFromEmailAsync(email);
        Assert.False(string.IsNullOrWhiteSpace(token));

        (string To, string Subject, string Body) mail;
        lock (FakeEmail.Sent) mail = FakeEmail.Sent.Single(m => m.To == email);
        Assert.Contains("http://localhost:5173/reset-password", mail.Body);
        Assert.Contains("15 minutes", mail.Body);
        Assert.DoesNotContain(OldPassword, mail.Body);
    }

    [Fact]
    public async Task Forgot_ForAnUnknownEmail_LooksIdenticalAndSendsNothing()
    {
        var known = await NewUserAsync();
        var unknown = $"nobody-{Guid.NewGuid():N}@test.local";

        var a = await ForgotAsync(known);
        var b = await ForgotAsync(unknown);

        Assert.Equal(a.StatusCode, b.StatusCode);
        Assert.Equal(await a.Content.ReadAsStringAsync(), await b.Content.ReadAsStringAsync());

        await TokenFromEmailAsync(known);       // the real one does get mail
        await Task.Delay(300);
        Assert.Equal(0, MailCount(unknown));    // the unknown one does not
    }

    [Fact]
    public async Task Forgot_ForADeactivatedAccount_SendsNothing()
    {
        var email = await NewUserAsync(active: false);

        Assert.Equal(HttpStatusCode.OK, (await ForgotAsync(email)).StatusCode);

        await Task.Delay(300);
        Assert.Equal(0, MailCount(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Forgot_WithAMalformedEmail_IsRejected(string email)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ForgotAsync(email)).StatusCode);
    }

    [Fact]
    public async Task OnlyAHashOfTheTokenIsStored_AndItExpiresInFifteenMinutes()
    {
        var email = await NewUserAsync();
        var token = await RequestTokenAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.UserId).SingleAsync();
        var row = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == userId);

        Assert.NotEqual(token, row.TokenHash);
        Assert.DoesNotContain(token, row.TokenHash);
        Assert.Equal(64, row.TokenHash.Length); // SHA-256 as hex
        Assert.False(row.IsUsed);
        Assert.InRange((row.ExpiryDate - row.CreatedAt).TotalMinutes, 14.99, 15.01);
    }

    // ----------------------------------------------------------------- reset

    [Fact]
    public async Task Reset_WithAValidToken_ChangesThePassword_ForAdminsToo()
    {
        foreach (var role in new[] { "Admin", "Member" })
        {
            var email = await NewUserAsync(role);
            var token = await RequestTokenAsync(email);

            var response = await ResetAsync(email, token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // 9. The new password logs in, with the role unchanged.
            var login = await LoginAsync(email, NewPassword);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Contains(role, await login.Content.ReadAsStringAsync());

            // 10. The old password no longer works.
            Assert.Equal(HttpStatusCode.Conflict, (await LoginAsync(email, OldPassword, "d2")).StatusCode);
        }
    }

    [Fact]
    public async Task Reset_WithAWrongToken_IsRejected_AndNothingChanges()
    {
        var email = await NewUserAsync();
        await RequestTokenAsync(email);

        var response = await ResetAsync(email, "definitely-not-the-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid or has expired", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task Reset_WithAnExpiredToken_IsRejected()
    {
        var email = await NewUserAsync();
        var token = await RequestTokenAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.UserId).SingleAsync();
            await db.PasswordResetTokens.Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.ExpiryDate, DateTime.UtcNow.AddMinutes(-1)));
        }

        var response = await ResetAsync(email, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("expired", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, OldPassword)).StatusCode);
    }

    [Fact]
    public async Task Reset_TokenCannotBeUsedTwice()
    {
        var email = await NewUserAsync();
        var token = await RequestTokenAsync(email);

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(email, token, "First-New#2026")).StatusCode);

        var again = await ResetAsync(email, token, "Second-New#2026");

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("already been used", await again.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, "First-New#2026")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await LoginAsync(email, "Second-New#2026", "d2")).StatusCode);
    }

    [Fact]
    public async Task ANewRequest_InvalidatesThePreviousLink()
    {
        var email = await NewUserAsync();
        var first = await RequestTokenAsync(email);
        var second = await RequestTokenAsync(email, skip: 1);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(email, first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(email, second)).StatusCode);
    }

    [Fact]
    public async Task Reset_WithTheTokenOfAnotherAccount_IsRejected()
    {
        var owner = await NewUserAsync();
        var other = await NewUserAsync();
        var ownerToken = await RequestTokenAsync(owner);

        var response = await ResetAsync(other, ownerToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(other, OldPassword)).StatusCode);

        // ...and the real owner's token is still good.
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(owner, ownerToken)).StatusCode);
    }

    [Fact]
    public async Task Reset_WithAWeakOrMismatchedPassword_IsRejected_AndKeepsTheTokenUsable()
    {
        var email = await NewUserAsync();
        var token = await RequestTokenAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(email, token, "abc")).StatusCode);          // too short
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(email, token, NewPassword, "Different")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(email, token, "")).StatusCode);

        // A rejected attempt did not burn the link.
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(email, token)).StatusCode);
    }

    [Fact]
    public async Task Reset_SignsOutEverySessionOfTheAccount()
    {
        var email = await NewUserAsync();
        var login = await LoginAsync(email, OldPassword, "laptop");
        var jwt = (await login.Content.ReadFromJsonAsync<Auth>())!.Token;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/sessions")).StatusCode);

        var token = await RequestTokenAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(email, token)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/sessions")).StatusCode);
    }

    [Fact]
    public async Task Reset_SimultaneousAttemptsWithOneToken_SucceedExactlyOnce()
    {
        var email = await NewUserAsync();
        var token = await RequestTokenAsync(email);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(i => ResetAsync(email, token, $"Parallel-{i}#2026")));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task EmailMatchingIsCaseInsensitive()
    {
        var email = await NewUserAsync();
        var token = await RequestTokenAsync(email);

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(email.ToUpperInvariant(), token)).StatusCode);
    }

    // ----------------------------------------------------------- configuration

    [Fact]
    public async Task WithoutAFrontendUrl_NoEmailIsSent_ButTheAnswerIsUnchanged()
    {
        var email = await NewUserAsync();
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordReset:FrontendResetUrl"] = "" })));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.Delay(300);
        Assert.Equal(0, MailCount(email));
    }

    [Fact]
    public async Task TheLinkComesFromConfiguration_NotFromTheRequestHost()
    {
        var email = await NewUserAsync();
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(new { email })
        };
        request.Headers.Host = "evil.example";
        request.Headers.Add("X-Forwarded-Host", "evil.example");

        (await client.SendAsync(request)).EnsureSuccessStatusCode();
        await TokenFromEmailAsync(email);

        (string To, string Subject, string Body) mail;
        lock (FakeEmail.Sent) mail = FakeEmail.Sent.Single(m => m.To == email);
        Assert.DoesNotContain("evil.example", mail.Body);
        Assert.Contains("http://localhost:5173/reset-password", mail.Body);
    }

    [Fact]
    public async Task TheEmailCooldown_StopsRepeatedRequestsFromFloodingAnInbox()
    {
        var email = await NewUserAsync();
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordReset:ResendCooldownSeconds"] = "60" })));
        var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        }

        await TokenFromEmailAsync(email);
        await Task.Delay(300);
        Assert.Equal(1, MailCount(email));
    }

    [Fact]
    public async Task TheEndpoints_AreRateLimitedPerClient()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordReset:RequestsPerWindow"] = "3" })));
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/auth/forgot-password",
                new { email = $"someone{i}@test.local" })).StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }
}
