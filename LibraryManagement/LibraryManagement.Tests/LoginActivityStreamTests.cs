using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LibraryManagement.Data;
using LibraryManagement.DTOs.Auth;
using LibraryManagement.Models;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Tests;

public sealed class LoginActivityStreamTests
{
    [Fact]
    public async Task AdminStreamReceivesPersistedLoginAndLogoutEvents_AndRejectsAnonymousClients()
    {
        using var factory = new LibraryApiFactory();
        var email = $"activity-{Guid.NewGuid():N}@test.local";
        using var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Admin", email, password = "Test@12345" });
        registration.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
            user.Role = "Admin";
            await db.SaveChangesAsync();
        }
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test@12345" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/login-history/events")).StatusCode);

        using var streamResponse = await client.GetAsync("/api/login-history/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
        await using var stream = await streamResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var sessionId = Guid.NewGuid().ToString("N");
        LoginHistory entry;
        using (var scope = factory.Services.CreateScope())
        {
            var history = scope.ServiceProvider.GetRequiredService<ILoginHistoryService>();
            entry = new LoginHistory
            {
                Username = email,
                LoginDateTime = DateTime.UtcNow,
                LoginStatus = "Success",
                SessionId = sessionId,
                SessionExpiresAt = DateTime.UtcNow.AddMinutes(60),
                SessionStatus = "Active"
            };
            await history.RecordLoginAsync(entry);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var loginEvent = await ReadEventAsync(reader, timeout.Token);
        Assert.Equal("login:new", loginEvent.EventName);
        Assert.Contains($"\"loginLogId\":{entry.LoginLogId}", loginEvent.Data);
        Assert.DoesNotContain("sessionId", loginEvent.Data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", loginEvent.Data, StringComparison.OrdinalIgnoreCase);

        using (var scope = factory.Services.CreateScope())
        {
            var history = scope.ServiceProvider.GetRequiredService<ILoginHistoryService>();
            var updated = await history.RecordLogoutAsync(sessionId, DateTime.UtcNow);
            Assert.Equal("Logged Out", updated!.SessionStatus);
        }
        var logoutEvent = await ReadEventAsync(reader, timeout.Token);
        Assert.Equal("logout:update", logoutEvent.EventName);
        Assert.Contains($"\"loginLogId\":{entry.LoginLogId}", logoutEvent.Data);
        Assert.Contains("Logged Out", logoutEvent.Data);
    }

    private static async Task<(string EventName, string Data)> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? eventName = null;
        var data = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) throw new EndOfStreamException("The activity stream closed before delivering an event.");
            if (line.Length == 0)
            {
                if (eventName is not null && data.Count > 0) return (eventName, string.Join('\n', data));
                continue;
            }
            if (line.StartsWith("event:", StringComparison.Ordinal)) eventName = line[6..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal)) data.Add(line[5..].TrimStart());
        }
    }
}
