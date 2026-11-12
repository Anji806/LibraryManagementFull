using LibraryManagement.Data;
using LibraryManagement.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Tests;

public class SqliteDevSchemaTests
{
    [Fact]
    public async Task ExistingDatabaseWithoutSessionTables_GetsThemWithoutLosingData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lm-dev-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False")
            .Options;

        try
        {
            // An "old" database: created before the session feature existed.
            await using (var old = new ApplicationDbContext(options))
            {
                await old.Database.EnsureCreatedAsync();
                old.Users.Add(new User { FullName = "Keep", Email = "keep@test.local", PasswordHash = "x" });
                await old.SaveChangesAsync();
                await old.Database.ExecuteSqlRawAsync("DROP TABLE \"UserSessions\"");
                await old.Database.ExecuteSqlRawAsync("DROP TABLE \"PendingLogins\"");
            }

            await using var db = new ApplicationDbContext(options);

            await SqliteDevSchema.EnsureLatestAsync(db);
            await SqliteDevSchema.EnsureLatestAsync(db); // idempotent

            var user = await db.Users.SingleAsync(u => u.Email == "keep@test.local");

            db.UserSessions.Add(new UserSession
            {
                UserId = user.UserId,
                SessionId = "s1",
                DeviceName = "Test",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync();

            Assert.Equal(1, await db.UserSessions.CountAsync());
            Assert.Equal(0, await db.PendingLogins.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
