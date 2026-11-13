using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Data;

/// <summary>
/// Local-development (SQLite) only. EnsureCreated builds the whole schema for
/// an empty file but does nothing when the file already has tables, so tables
/// added to the model later would be missing. This adds them with
/// CREATE ... IF NOT EXISTS: additive, idempotent, never touches existing data.
/// (PostgreSQL uses EF Core migrations instead.)
/// </summary>
public static class SqliteDevSchema
{
    public static async Task EnsureLatestAsync(ApplicationDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        foreach (var statement in SessionTables)
        {
            await db.Database.ExecuteSqlRawAsync(statement);
        }

        await AddMissingColumnsAsync(db, "BorrowTransactions", BorrowColumns);
    }

    // Columns added to existing tables after the file was first created.
    // Existing rows keep their data: DueDate stays NULL, fines default to 0.
    private static readonly (string Name, string Definition)[] BorrowColumns =
    [
        ("DueDate", "TEXT NULL"),
        ("FineAmount", "TEXT NOT NULL DEFAULT '0.0'"),
        ("FinePaid", "TEXT NOT NULL DEFAULT '0.0'")
    ];

    private static async Task AddMissingColumnsAsync(
        ApplicationDbContext db,
        string table,
        (string Name, string Definition)[] columns)
    {
        var existing = (await db.Database
                .SqlQueryRaw<string>($"SELECT name AS Value FROM pragma_table_info('{table}')")
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, definition) in columns.Where(c => !existing.Contains(c.Name)))
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{name}\" {definition}");
        }
    }

    private static readonly string[] SessionTables =
    [
        """
        CREATE TABLE IF NOT EXISTS "UserSessions" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_UserSessions" PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "SessionId" TEXT NOT NULL,
            "DeviceId" TEXT NULL,
            "DeviceName" TEXT NOT NULL,
            "Browser" TEXT NULL,
            "OperatingSystem" TEXT NULL,
            "IpAddress" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "LastActivityAt" TEXT NOT NULL,
            "ExpiresAt" TEXT NOT NULL,
            "IsRevoked" INTEGER NOT NULL,
            "RevokedAt" TEXT NULL,
            CONSTRAINT "FK_UserSessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserSessions_SessionId" ON "UserSessions" ("SessionId")""",
        """CREATE INDEX IF NOT EXISTS "IX_UserSessions_UserId_IsRevoked_ExpiresAt" ON "UserSessions" ("UserId", "IsRevoked", "ExpiresAt")""",
        """
        CREATE TABLE IF NOT EXISTS "PendingLogins" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_PendingLogins" PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "TokenHash" TEXT NOT NULL,
            "DeviceId" TEXT NULL,
            "DeviceName" TEXT NOT NULL,
            "Browser" TEXT NULL,
            "OperatingSystem" TEXT NULL,
            "IpAddress" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "ExpiresAt" TEXT NOT NULL,
            "ConsumedAt" TEXT NULL,
            CONSTRAINT "FK_PendingLogins_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
        )
        """,
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_PendingLogins_TokenHash" ON "PendingLogins" ("TokenHash")""",
        """CREATE INDEX IF NOT EXISTS "IX_PendingLogins_ExpiresAt" ON "PendingLogins" ("ExpiresAt")"""
    ];
}
