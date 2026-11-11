using LibraryManagement.Data;
using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Tests;

/// <summary>
/// Repository-level checks of the EF Core data access, run through the same
/// database the API tests use (SQLite, or PostgreSQL via TEST_DATABASE_URL).
/// </summary>
public sealed class LoginHistoryRepositoryTests
{
    [Fact]
    public async Task LoginHistory_SupportsIdentityPagingAndLogout()
    {
        using var factory = new LibraryApiFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = new LoginHistoryRepository(context);
        var sessionId = Guid.NewGuid().ToString("N");

        await repository.InsertLoginHistoryAsync(new LoginHistory
        {
            Username = "reader@example.test",
            LoginDateTime = DateTime.UtcNow.AddMinutes(-1),
            LoginStatus = "Success",
            SessionId = sessionId,
            SessionStatus = "Active"
        });
        await repository.InsertLoginHistoryAsync(new LoginHistory
        {
            Username = "reader@example.test",
            LoginDateTime = DateTime.UtcNow,
            LoginStatus = "Failed",
            FailureReason = "Invalid credentials."
        });

        var page = await repository.GetLoginHistoryAsync(new LoginHistoryQuery
        {
            Username = "READER",
            Page = 1,
            PageSize = 1
        });

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.True(page.Items[0].LoginLogId > 0);
        Assert.Equal("Failed", page.Items[0].LoginStatus);

        var logoutAt = DateTime.UtcNow;
        Assert.NotNull(await repository.RecordLogoutAsync(sessionId, logoutAt));
        // The session is no longer active, so a second logout is rejected.
        Assert.Null(await repository.RecordLogoutAsync(sessionId, logoutAt));

        var loggedOut = await context.LoginHistory.AsNoTracking()
            .SingleAsync(x => x.SessionId == sessionId);
        Assert.Equal("Logged Out", loggedOut.SessionStatus);
        Assert.True(Math.Abs((logoutAt - loggedOut.LogoutDateTime!.Value).TotalMilliseconds) < 5);
    }

    [Fact]
    public async Task DeleteLoginHistory_RemovesOnlyRequestedRowsWithoutDeletingTheirUser()
    {
        using var factory = new LibraryApiFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            FullName = "Auditor",
            Email = "auditor@example.test",
            PasswordHash = "hash",
            Role = "Admin"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new LoginHistoryRepository(context);
        var selected = new[]
        {
            new LoginHistory { UserId = user.UserId, Username = user.Email, LoginDateTime = DateTime.UtcNow, LoginStatus = "Success", SessionId = Guid.NewGuid().ToString("N"), SessionStatus = "Active" },
            new LoginHistory { Username = "unknown@example.test", LoginDateTime = DateTime.UtcNow, LoginStatus = "Failed", FailureReason = "Invalid credentials." }
        };
        var keep = new LoginHistory { Username = "keep@example.test", LoginDateTime = DateTime.UtcNow, LoginStatus = "Failed", FailureReason = "Invalid credentials." };
        foreach (var record in selected.Append(keep)) await repository.InsertLoginHistoryAsync(record);

        // A stale selection deletes nothing.
        Assert.Equal(-1, await repository.DeleteLoginHistoryAsync([selected[0].LoginLogId, long.MaxValue]));
        Assert.True(await context.LoginHistory.AnyAsync(x => x.LoginLogId == selected[0].LoginLogId));

        var deletedCount = await repository.DeleteLoginHistoryAsync(selected.Select(record => record.LoginLogId).ToArray());

        Assert.Equal(2, deletedCount);
        Assert.True(await context.LoginHistory.AnyAsync(x => x.LoginLogId == keep.LoginLogId));
        Assert.True(await context.Users.AnyAsync(x => x.UserId == user.UserId));
        Assert.False(await context.LoginHistory.AnyAsync(x => x.LoginLogId == selected[0].LoginLogId || x.LoginLogId == selected[1].LoginLogId));
    }

    [Fact]
    public async Task Email_IsUniqueIgnoringCase()
    {
        using var factory = new LibraryApiFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Users.Add(new User { FullName = "A", Email = "Case@Example.Test", PasswordHash = "x" });
        await context.SaveChangesAsync();

        Assert.True(await context.Users.AnyAsync(u => u.Email == "case@example.test"));

        context.Users.Add(new User { FullName = "B", Email = "CASE@example.test", PasswordHash = "x" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
