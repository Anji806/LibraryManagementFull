using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LibraryManagement.Data;
using LibraryManagement.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LibraryManagement.Tests;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public System.Data.Common.DbConnection CreateConnection()
        => new SqliteConnection(_connectionString);

    public string LastInsertIdQuery => "SELECT last_insert_rowid()";
}

public class LibraryApiFactory : WebApplicationFactory<Program>
{
    // A uniquely named shared-cache in-memory DB lets the EF context (used to
    // seed data) and the Dapper repositories open their own connections to
    // the same database. The open connection below keeps it alive; Foreign
    // Keys=True enforces the same constraints that caused the original 500.
    private readonly string _connectionString =
        $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared;Foreign Keys=True";

    private readonly SqliteConnection _connection;

    public LibraryApiFactory()
    {
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TEST_KEY_TEST_KEY_TEST_KEY_TEST_KEY_1234567890",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiryMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType.FullName!.Contains("IDbContextOptionsConfiguration"))
                .ToList();

            foreach (var d in toRemove)
            {
                services.Remove(d);
            }

            services.AddDbContext<ApplicationDbContext>(o =>
                o.UseSqlite(_connectionString));

            services.RemoveAll<IDbConnectionFactory>();
            services.AddSingleton<IDbConnectionFactory>(
                new SqliteConnectionFactory(_connectionString));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>()
                .Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public class DeleteBookTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";

    private readonly LibraryApiFactory _factory;

    public DeleteBookTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClientForAsync(string role)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = role, email, password = Password });
        register.EnsureSuccessStatusCode();

        if (role != "Member")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<LoginResult>())!.Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private async Task<int> SeedBookAsync(string? borrowStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N");

        var book = new Book
        {
            Title = "Book " + suffix,
            ISBN = suffix,
            TotalCopies = 1,
            AvailableCopies = 1,
            PublishedYear = 2020,
            Author = new Author { AuthorName = "A " + suffix },
            Category = new Category { CategoryName = "C " + suffix }
        };

        if (borrowStatus != null)
        {
            var member = new User
            {
                FullName = "m",
                Email = $"m-{suffix}@test.local",
                PasswordHash = "x",
                Role = "Member"
            };

            book.BorrowTransactions.Add(new BorrowTransaction
            {
                Member = member,
                BorrowDate = DateTime.UtcNow.AddDays(-5),
                ReturnDate = borrowStatus == "Returned" ? DateTime.UtcNow : null,
                Status = borrowStatus
            });
        }

        db.Books.Add(book);
        await db.SaveChangesAsync();

        return book.BookId;
    }

    private async Task<bool> BookExistsAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Books.AnyAsync(b => b.BookId == id);
    }

    [Fact]
    public async Task Delete_BookWithNoHistory_AsAdmin_ReturnsNoContent()
    {
        var client = await ClientForAsync("Admin");
        var id = await SeedBookAsync(null);

        var response = await client.DeleteAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await BookExistsAsync(id));
    }

    [Theory]
    [InlineData("Admin", "Returned")]
    [InlineData("Admin", "Borrowed")]
    public async Task Delete_BookWithBorrowHistory_ReturnsConflict(
        string role,
        string status)
    {
        var client = await ClientForAsync(role);
        var id = await SeedBookAsync(status);

        var response = await client.DeleteAsync($"/api/books/{id}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("borrowing history", body);
        Assert.DoesNotContain("FOREIGN KEY", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLite", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(await BookExistsAsync(id));
    }

    [Fact]
    public async Task Delete_MissingBook_ReturnsNotFound()
    {
        var client = await ClientForAsync("Admin");

        var response = await client.DeleteAsync("/api/books/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsLibrarian_ReturnsForbidden()
    {
        var client = await ClientForAsync("Librarian");
        var id = await SeedBookAsync(null);

        var response = await client.DeleteAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await BookExistsAsync(id));
    }

    [Fact]
    public async Task Delete_AsMember_ReturnsForbidden()
    {
        var client = await ClientForAsync("Member");
        var id = await SeedBookAsync(null);

        var response = await client.DeleteAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await BookExistsAsync(id));
    }

    [Fact]
    public async Task Delete_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var id = await SeedBookAsync(null);

        var response = await client.DeleteAsync($"/api/books/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(await BookExistsAsync(id));
    }

    private sealed record LoginResult(string Token);
}
