using Dapper;
using Microsoft.Data.Sqlite;

namespace LibraryManagement.Data;

/// <summary>Creates the Dapper application's SQLite schema on first startup.</summary>
public static class BookPreviewContentInitializer
{
    public static async Task InitializeAsync(
        IDbConnectionFactory connectionFactory,
        string contentRootPath)
    {
        var schemaPath = Path.Combine(contentRootPath, "database", "sqlite_schema.sql");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(AppContext.BaseDirectory, "database", "sqlite_schema.sql");
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException("SQLite schema script was not found.", schemaPath);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();

        if (connection is not SqliteConnection)
            throw new InvalidOperationException("The API requires a SQLite connection.");

        await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await connection.ExecuteAsync("PRAGMA foreign_keys=ON;");
        await connection.ExecuteAsync("PRAGMA busy_timeout=30000;");
        await connection.ExecuteAsync(await File.ReadAllTextAsync(schemaPath));
        // Upgrade the legacy one-category relationship in place and preserve it.
        await connection.ExecuteAsync(@"INSERT OR IGNORE INTO BookCategories (BookId, CategoryId)
            SELECT BookId, CategoryId FROM Books;");
        // Upgrade existing installations without replacing or discarding login history.
        var columns = (await connection.QueryAsync<SchemaColumn>("PRAGMA table_info(LoginHistory);"))
            .Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!columns.Contains("LogoutDateTime")) await connection.ExecuteAsync("ALTER TABLE LoginHistory ADD COLUMN LogoutDateTime TEXT NULL;");
        if (!columns.Contains("SessionExpiresAt")) await connection.ExecuteAsync("ALTER TABLE LoginHistory ADD COLUMN SessionExpiresAt TEXT NULL;");
        if (!columns.Contains("SessionId")) await connection.ExecuteAsync("ALTER TABLE LoginHistory ADD COLUMN SessionId TEXT NULL;");
        if (!columns.Contains("SessionStatus"))
        {
            await connection.ExecuteAsync("ALTER TABLE LoginHistory ADD COLUMN SessionStatus TEXT NOT NULL DEFAULT 'Unknown';");
            await connection.ExecuteAsync("UPDATE LoginHistory SET SessionStatus = CASE WHEN LoginStatus = 'Failed' THEN 'Failed' ELSE 'Unknown' END;");
        }
        await connection.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_LoginHistory_SessionId ON LoginHistory(SessionId) WHERE SessionId IS NOT NULL;");
    }

    private sealed class SchemaColumn { public string Name { get; set; } = string.Empty; }
}
