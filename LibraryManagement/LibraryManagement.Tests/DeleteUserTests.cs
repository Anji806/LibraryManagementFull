using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LibraryManagement.Data;
using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LibraryManagement.Tests;

/// <summary>Shares LibraryApiFactory defined in DeleteBookTests.cs.</summary>
public class DeleteUserTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";

    private sealed record LoginResult(string Token);

    private readonly LibraryApiFactory _factory;

    public DeleteUserTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, int UserId)> ClientForAsync(string role)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = role, email, password = Password })).EnsureSuccessStatusCode();

        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = role;
            await db.SaveChangesAsync();
            userId = user.UserId;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<LoginResult>())!.Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return (client, userId);
    }

    private async Task<int> SeedMemberAsync(string? borrowStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N");

        var member = new User
        {
            FullName = "Member " + suffix,
            Email = $"m-{suffix}@test.local",
            PasswordHash = "x",
            Role = "Member"
        };
        db.Users.Add(member);

        if (borrowStatus != null)
        {
            db.BorrowTransactions.Add(new BorrowTransaction
            {
                Member = member,
                BorrowDate = DateTime.UtcNow.AddDays(-5),
                ReturnDate = borrowStatus == "Returned" ? DateTime.UtcNow : null,
                Status = borrowStatus,
                Book = new Book
                {
                    Title = "Book " + suffix,
                    ISBN = suffix,
                    TotalCopies = 1,
                    AvailableCopies = 1,
                    PublishedYear = 2020,
                    Author = new Author { AuthorName = "A " + suffix },
                    Category = new Category { CategoryName = "C " + suffix }
                }
            });
        }

        await db.SaveChangesAsync();
        return member.UserId;
    }

    private async Task<bool> UserExistsAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.AnyAsync(u => u.UserId == id);
    }

    [Fact]
    public async Task Delete_UserWithNoHistory_AsAdmin_ReturnsNoContent()
    {
        var (client, _) = await ClientForAsync("Admin");
        var id = await SeedMemberAsync(null);

        var response = await client.DeleteAsync($"/api/users/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await UserExistsAsync(id));
    }

    [Theory]
    [InlineData("Returned", "returned borrowing record")]
    [InlineData("Borrowed", "not yet returned")]
    public async Task Delete_UserWithBorrowHistory_ReturnsConflictWithClearMessage(
        string status,
        string expectedText)
    {
        var (client, _) = await ClientForAsync("Admin");
        var id = await SeedMemberAsync(status);

        var response = await client.DeleteAsync($"/api/users/{id}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(expectedText, body);
        Assert.Contains("deactivate", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(await UserExistsAsync(id));
    }

    [Fact]
    public async Task Delete_OwnAccount_ReturnsBadRequest()
    {
        var (client, selfId) = await ClientForAsync("Admin");

        var response = await client.DeleteAsync($"/api/users/{selfId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await UserExistsAsync(selfId));
    }

    [Fact]
    public async Task Delete_MissingUser_ReturnsNotFound()
    {
        var (client, _) = await ClientForAsync("Admin");

        var response = await client.DeleteAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Librarian")]
    [InlineData("Member")]
    public async Task Delete_AsNonAdmin_ReturnsForbidden(string role)
    {
        var (client, _) = await ClientForAsync(role);
        var id = await SeedMemberAsync(null);

        var response = await client.DeleteAsync($"/api/users/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await UserExistsAsync(id));
    }

    [Fact]
    public async Task Delete_Unauthenticated_ReturnsUnauthorized()
    {
        var id = await SeedMemberAsync(null);

        var response = await _factory.CreateClient().DeleteAsync($"/api/users/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(await UserExistsAsync(id));
    }
}
