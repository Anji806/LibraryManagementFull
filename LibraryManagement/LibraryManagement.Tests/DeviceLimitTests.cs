using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LibraryManagement.Data;
using LibraryManagement.Helpers;
using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Tests;

/// <summary>Two-active-devices-per-user rule, challenge flow and session enforcement.</summary>
public class DeviceLimitTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";

    private readonly LibraryApiFactory _factory;

    public DeviceLimitTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    private sealed record AuthBody(string Token, int UserId, string Role);

    private sealed record SessionBody(int Id, string DeviceName, bool IsCurrent);

    private sealed record LimitBody(
        string Code,
        string ChallengeToken,
        int ExpiresInSeconds,
        List<SessionBody> Sessions);

    /// <summary>Registers a user with the given role and clears the registration session.</summary>
    private async Task<string> NewUserAsync(string role = "Member")
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";

        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = role, email, password = Password, deviceId = "seed" }))
            .EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.UserSessions.Where(s => s.UserId == user.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.IsRevoked, true));
        await db.SaveChangesAsync();

        return email;
    }

    private Task<HttpResponseMessage> LoginAsync(
        string email, string device, string password = Password)
        => _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email, password, deviceId = device, deviceName = $"Device {device}" });

    private async Task<string> LoginOkAsync(string email, string device)
    {
        var response = await LoginAsync(email, device);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthBody>())!.Token;
    }

    private async Task<LimitBody> LoginLimitedAsync(string email, string device)
    {
        var response = await LoginAsync(email, device);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LimitBody>();
        Assert.Equal("DEVICE_LIMIT_REACHED", body!.Code);
        return body;
    }

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpStatusCode> ProbeAsync(string token)
        => (await ClientWith(token).GetAsync("/api/auth/sessions")).StatusCode;

    private Task<HttpResponseMessage> CompleteAsync(string challenge, int sessionId)
        => _factory.CreateClient().PostAsJsonAsync("/api/auth/complete-login",
            new { challengeToken = challenge, sessionToRevokeId = sessionId });

    private async Task<int> ActiveCountAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var userId = await db.Users.Where(u => u.Email == email)
            .Select(u => u.UserId).SingleAsync();
        return await db.UserSessions.CountAsync(s =>
            s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Admin")]
    [InlineData("Librarian")]
    public async Task FirstTwoDevicesSignIn_ThirdIsHeldWithDeviceList_ForEveryRole(string role)
    {
        var email = await NewUserAsync(role);

        var tokenA = await LoginOkAsync(email, "A");
        var tokenB = await LoginOkAsync(email, "B");
        var limit = await LoginLimitedAsync(email, "C");

        Assert.Equal(2, limit.Sessions.Count);
        Assert.Contains(limit.Sessions, s => s.DeviceName == "Device A");
        Assert.Contains(limit.Sessions, s => s.DeviceName == "Device B");
        Assert.False(string.IsNullOrEmpty(limit.ChallengeToken));
        Assert.True(limit.ExpiresInSeconds > 0);

        // Both existing devices keep working; the third got no token.
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(tokenA));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(tokenB));
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task ThirdDeviceResponse_ContainsNoTokenAndNoSecrets()
    {
        var email = await NewUserAsync();
        await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");

        var response = await LoginAsync(email, "C");
        var text = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("\"token\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongPassword_NeverRevealsTheDeviceList()
    {
        var email = await NewUserAsync();
        await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");

        var response = await LoginAsync(email, "C", password: "wrong-password");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("DEVICE_LIMIT_REACHED", text);
        Assert.DoesNotContain("Device A", text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SelectingADevice_LogsItOutAndLetsTheThirdDeviceIn(int index)
    {
        var email = await NewUserAsync();
        var tokens = new[] { await LoginOkAsync(email, "A"), await LoginOkAsync(email, "B") };
        var limit = await LoginLimitedAsync(email, "C");

        var chosen = limit.Sessions.Single(s => s.DeviceName == $"Device {"AB"[index]}");
        var response = await CompleteAsync(limit.ChallengeToken, chosen.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthBody>())!;
        Assert.False(string.IsNullOrEmpty(auth.Token));

        // The chosen device lost access immediately; the other and the new one work.
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(tokens[index]));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(tokens[1 - index]));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(auth.Token));
        Assert.Equal(2, await ActiveCountAsync(email));

        var sessions = await ClientWith(auth.Token)
            .GetFromJsonAsync<List<SessionBody>>("/api/auth/sessions");
        Assert.Equal(2, sessions!.Count);
        Assert.Single(sessions, s => s.IsCurrent);
    }

    [Fact]
    public async Task Challenge_IsSingleUse()
    {
        var email = await NewUserAsync();
        await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");
        var limit = await LoginLimitedAsync(email, "C");

        var first = await CompleteAsync(limit.ChallengeToken, limit.Sessions[0].Id);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replaying it (even to revoke the other device) must fail.
        var replay = await CompleteAsync(limit.ChallengeToken, limit.Sessions[1].Id);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task Challenge_ExpiresAndCannotBeGuessed()
    {
        var email = await NewUserAsync();
        await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");
        var limit = await LoginLimitedAsync(email, "C");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await CompleteAsync("not-a-real-challenge", limit.Sessions[0].Id)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.PendingLogins.ExecuteUpdateAsync(set =>
                set.SetProperty(p => p.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await CompleteAsync(limit.ChallengeToken, limit.Sessions[0].Id)).StatusCode);
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task Challenge_CannotRevokeAnotherUsersSession()
    {
        var victim = await NewUserAsync();
        var victimToken = await LoginOkAsync(victim, "V1");
        var victimSessions = await ClientWith(victimToken)
            .GetFromJsonAsync<List<SessionBody>>("/api/auth/sessions");

        var attacker = await NewUserAsync();
        await LoginOkAsync(attacker, "A");
        await LoginOkAsync(attacker, "B");
        var limit = await LoginLimitedAsync(attacker, "C");

        var response = await CompleteAsync(limit.ChallengeToken, victimSessions![0].Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(victimToken));
    }

    [Fact]
    public async Task Logout_ReleasesTheDeviceSlot()
    {
        var email = await NewUserAsync();
        var tokenA = await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");
        await LoginLimitedAsync(email, "C");

        var logout = await ClientWith(tokenA).PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(tokenA));

        await LoginOkAsync(email, "C");
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task LogoutAll_RevokesEveryDevice()
    {
        var email = await NewUserAsync();
        var tokenA = await LoginOkAsync(email, "A");
        var tokenB = await LoginOkAsync(email, "B");

        Assert.Equal(HttpStatusCode.NoContent,
            (await ClientWith(tokenA).PostAsync("/api/auth/logout-all", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(tokenA));
        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(tokenB));
        Assert.Equal(0, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task ExpiredSessions_DoNotCountTowardTheLimitOrWork()
    {
        var email = await NewUserAsync();
        var tokenA = await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = await db.Users.Where(u => u.Email == email)
                .Select(u => u.UserId).SingleAsync();
            await db.UserSessions
                .Where(s => s.UserId == userId && s.DeviceId == "A")
                .ExecuteUpdateAsync(set =>
                    set.SetProperty(s => s.ExpiresAt, DateTime.UtcNow.AddMinutes(-5)));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(tokenA));
        await LoginOkAsync(email, "C");
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task SigningInAgainOnTheSameBrowser_ReplacesItsSessionInsteadOfUsingASlot()
    {
        var email = await NewUserAsync();
        var first = await LoginOkAsync(email, "A");
        var second = await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");

        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(first));
        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(second));
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task ChangingTheDeviceId_DoesNotBypassTheLimit()
    {
        var email = await NewUserAsync();
        await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");

        foreach (var invented in new[] { "C", "D", Guid.NewGuid().ToString("N") })
        {
            await LoginLimitedAsync(email, invented);
        }

        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task SimultaneousSignIns_NeverExceedTwoSessions()
    {
        var email = await NewUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => LoginAsync(email, $"race-{i}")));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(6, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task SimultaneousChallengeCompletion_CreatesOnlyOneSession()
    {
        var email = await NewUserAsync();
        await LoginOkAsync(email, "A");
        await LoginOkAsync(email, "B");
        var limit = await LoginLimitedAsync(email, "C");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => CompleteAsync(limit.ChallengeToken, limit.Sessions[0].Id)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(2, await ActiveCountAsync(email));
    }

    [Fact]
    public async Task ATokenWithoutASession_IsRejected()
    {
        var email = await NewUserAsync();

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            token = scope.ServiceProvider.GetRequiredService<JwtTokenGenerator>()
                .GenerateToken(user);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await ProbeAsync(token));
    }

    [Fact]
    public async Task RegistrationSignsTheNewUserIn()
    {
        var email = $"new-{Guid.NewGuid():N}@test.local";
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = "New", email, password = Password, deviceId = "R1" });

        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<AuthBody>())!.Token;

        Assert.Equal(HttpStatusCode.OK, await ProbeAsync(token));
    }
}
