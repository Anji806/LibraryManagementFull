using Dapper;
using LibraryManagement.Data;
using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Implementations;
using Microsoft.Data.Sqlite;

namespace LibraryManagement.Tests;

public sealed class SqliteMigrationTests
{
    [Fact]
    public async Task SqliteSchemaSupportsLoginHistoryIdentityPagingAndForeignKeys()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared;Foreign Keys=True";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var factory = new SqliteConnectionFactory(connectionString);
        var schema = Path.Combine(AppContext.BaseDirectory, "database", "sqlite_schema.sql");
        await BookPreviewContentInitializer.InitializeAsync(factory, AppContext.BaseDirectory);

        using var uow = new DapperUnitOfWork(factory);
        var repository = new LoginHistoryRepository(uow);
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
            Username = "reader",
            Page = 1,
            PageSize = 1
        });

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.True(page.Items[0].LoginLogId > 0);
        Assert.Equal("Failed", page.Items[0].LoginStatus);

        var connection = await uow.GetConnectionAsync();
        var logoutAt = DateTime.UtcNow;
        Assert.NotNull(await repository.RecordLogoutAsync(sessionId, logoutAt));
        var loggedOut = await connection.QuerySingleAsync<LoginHistory>("SELECT LogoutDateTime, SessionStatus FROM LoginHistory WHERE SessionId = @sessionId", new { sessionId });
        Assert.Equal("Logged Out", loggedOut.SessionStatus);
        Assert.Equal(logoutAt, loggedOut.LogoutDateTime);

        var foreignKeysEnabled = await connection.ExecuteScalarAsync<int>("PRAGMA foreign_keys;");
        Assert.Equal(1, foreignKeysEnabled);
        Assert.True(File.Exists(schema));
    }

    [Fact]
    public async Task DeleteLoginHistory_RemovesOnlyRequestedRowsWithoutDeletingTheirUser()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared;Foreign Keys=True";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var factory = new SqliteConnectionFactory(connectionString);
        await BookPreviewContentInitializer.InitializeAsync(factory, AppContext.BaseDirectory);
        using var uow = new DapperUnitOfWork(factory);
        var connection = await uow.GetConnectionAsync();
        var userId = await connection.ExecuteScalarAsync<int>("INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, Address) VALUES ('Auditor', 'auditor@example.test', 'hash', 'Admin', 1, ''); SELECT last_insert_rowid();");
        var repository = new LoginHistoryRepository(uow);
        var selected = new[]
        {
            new LoginHistory { UserId = userId, Username = "auditor@example.test", LoginDateTime = DateTime.UtcNow, LoginStatus = "Success", SessionId = Guid.NewGuid().ToString("N"), SessionStatus = "Active" },
            new LoginHistory { Username = "unknown@example.test", LoginDateTime = DateTime.UtcNow, LoginStatus = "Failed", FailureReason = "Invalid credentials." }
        };
        var keep = new LoginHistory { Username = "keep@example.test", LoginDateTime = DateTime.UtcNow, LoginStatus = "Failed", FailureReason = "Invalid credentials." };
        foreach (var record in selected.Append(keep)) await repository.InsertLoginHistoryAsync(record);

        Assert.Equal(-1, await repository.DeleteLoginHistoryAsync([selected[0].LoginLogId, long.MaxValue]));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LoginHistory WHERE LoginLogId = @id", new { id = selected[0].LoginLogId }));

        var deletedCount = await repository.DeleteLoginHistoryAsync(selected.Select(record => record.LoginLogId).ToArray());

        Assert.Equal(2, deletedCount);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LoginHistory WHERE LoginLogId = @id", new { id = keep.LoginLogId }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users WHERE UserId = @userId", new { userId }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LoginHistory WHERE LoginLogId IN @ids", new { ids = selected.Select(record => record.LoginLogId).ToArray() }));
    }
}
