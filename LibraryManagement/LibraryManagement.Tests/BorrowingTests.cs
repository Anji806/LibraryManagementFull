using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LibraryManagement.Data;
using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LibraryManagement.Tests;

/// <summary>
/// Integration tests covering Sections 36 & 37 of the instruction specification:
///   - Book CRUD and copy-count validation rules.
///   - Borrowing lifecycle: borrow, unavailable, duplicate, return, already-returned.
/// Shares the LibraryApiFactory defined in DeleteBookTests.cs.
/// </summary>
public class BorrowingTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";
    private readonly LibraryApiFactory _factory;

    public BorrowingTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Register a user, optionally elevate their role, then log in and return
    /// an authenticated HttpClient + the user's ID from the auth response.
    /// </summary>
    private async Task<(HttpClient Client, int UserId)> AuthorizedClientAsync(string role)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = role, email, password = Password });
        register.EnsureSuccessStatusCode();

        // Elevate role in DB when not already the default (Member)
        if (role != "Member")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password });
        loginResp.EnsureSuccessStatusCode();

        var auth = (await loginResp.Content.ReadFromJsonAsync<AuthResult>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Token);

        return (client, auth.UserId);
    }

    /// <summary>
    /// Seed a book directly in the database and return its BookId.
    /// <param name="availableCopies">Override available copies (defaults to totalCopies).</param>
    /// </summary>
    private async Task<int> SeedBookAsync(int totalCopies = 3, int? availableCopies = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N");

        var book = new Book
        {
            Title = "Book " + suffix,
            ISBN = suffix[..13],
            TotalCopies = totalCopies,
            AvailableCopies = availableCopies ?? totalCopies,
            PublishedYear = 2024,
            Author = new Author { AuthorName = "Author " + suffix },
            Category = new Category { CategoryName = "Category " + suffix },
        };

        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book.BookId;
    }

    /// <summary>Returns the current AvailableCopies value for a book.</summary>
    private async Task<int> GetAvailableCopiesAsync(int bookId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var book = await db.Books.FindAsync(bookId);
        return book!.AvailableCopies;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Section 37 — Borrowing Lifecycle Tests
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Borrowing an available book succeeds (200) and decrements AvailableCopies.
    /// </summary>
    [Fact]
    public async Task Borrow_AvailableBook_SucceedsAndDecrementsCopies()
    {
        var (libClient, _) = await AuthorizedClientAsync("Librarian");
        var (_, memberId) = await AuthorizedClientAsync("Member");
        var bookId = await SeedBookAsync(totalCopies: 2);

        var response = await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("borrowed successfully", body, StringComparison.OrdinalIgnoreCase);

        // AvailableCopies must have decreased by 1
        Assert.Equal(1, await GetAvailableCopiesAsync(bookId));
    }

    /// <summary>
    /// Borrowing a book where AvailableCopies = 0 returns 409 Conflict
    /// with a message about availability.
    /// BusinessRuleException → GlobalExceptionHandler → 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Borrow_UnavailableBook_ReturnsConflict()
    {
        var (libClient, _) = await AuthorizedClientAsync("Librarian");
        var (_, memberId) = await AuthorizedClientAsync("Member");

        // Seed a book with 0 available copies
        var bookId = await SeedBookAsync(totalCopies: 1, availableCopies: 0);

        var response = await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("available", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A member borrowing the same book twice (while an active borrow exists)
    /// returns 409 Conflict with a message about duplicate borrowing.
    /// BusinessRuleException → GlobalExceptionHandler → 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Borrow_DuplicateActiveBorrow_ReturnsConflict()
    {
        var (libClient, _) = await AuthorizedClientAsync("Librarian");
        var (_, memberId) = await AuthorizedClientAsync("Member");
        var bookId = await SeedBookAsync(totalCopies: 5);

        // First borrow succeeds
        var first = await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);
        first.EnsureSuccessStatusCode();

        // Second borrow on the same active transaction should fail
        var second = await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("already borrowed", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returning an active borrow succeeds (200), marks the transaction Returned,
    /// and increments AvailableCopies back.
    /// </summary>
    [Fact]
    public async Task Return_ActiveBorrow_SucceedsAndIncrementsCopies()
    {
        var (libClient, _) = await AuthorizedClientAsync("Librarian");
        var (_, memberId) = await AuthorizedClientAsync("Member");
        var bookId = await SeedBookAsync(totalCopies: 1);

        // Borrow the book first
        var borrowResp = await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);
        borrowResp.EnsureSuccessStatusCode();

        var borrowBody = await borrowResp.Content.ReadFromJsonAsync<BorrowResult>();
        var transactionId = borrowBody!.Transaction.TransactionId;

        // Confirm available copies dropped to 0
        Assert.Equal(0, await GetAvailableCopiesAsync(bookId));

        // Process the return
        var returnResp = await libClient.PutAsync(
            $"/api/borrowtransactions/return/{transactionId}", null);

        Assert.Equal(HttpStatusCode.OK, returnResp.StatusCode);
        var returnBody = await returnResp.Content.ReadAsStringAsync();
        Assert.Contains("returned successfully", returnBody, StringComparison.OrdinalIgnoreCase);

        // Available copies must be restored
        Assert.Equal(1, await GetAvailableCopiesAsync(bookId));
    }

    /// <summary>
    /// Returning a book that has already been returned returns 409 Conflict.
    /// BusinessRuleException → GlobalExceptionHandler → 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Return_AlreadyReturnedBook_ReturnsConflict()
    {
        var (libClient, _) = await AuthorizedClientAsync("Librarian");
        var (_, memberId) = await AuthorizedClientAsync("Member");
        var bookId = await SeedBookAsync(totalCopies: 2);

        // Borrow
        var borrowResp = await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);
        borrowResp.EnsureSuccessStatusCode();
        var borrowBody = await borrowResp.Content.ReadFromJsonAsync<BorrowResult>();
        var transactionId = borrowBody!.Transaction.TransactionId;

        // First return succeeds
        var firstReturn = await libClient.PutAsync(
            $"/api/borrowtransactions/return/{transactionId}", null);
        firstReturn.EnsureSuccessStatusCode();

        // Second return on the same transaction should fail
        var secondReturn = await libClient.PutAsync(
            $"/api/borrowtransactions/return/{transactionId}", null);

        Assert.Equal(HttpStatusCode.Conflict, secondReturn.StatusCode);
        var body = await secondReturn.Content.ReadAsStringAsync();
        Assert.Contains("already been returned", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Member borrowing on behalf of another member (different memberId) is Forbidden.
    /// </summary>
    [Fact]
    public async Task Borrow_MemberBorrowingForOtherMember_ReturnsForbidden()
    {
        var (memberClient, _) = await AuthorizedClientAsync("Member");
        var (_, otherMemberId) = await AuthorizedClientAsync("Member");
        var bookId = await SeedBookAsync(totalCopies: 3);

        var response = await memberClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={otherMemberId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Section 36 — Book Validation Tests
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Admin can create a book and the response is 201 Created.
    /// AvailableCopies is automatically set to TotalCopies.
    /// </summary>
    [Fact]
    public async Task CreateBook_AsAdmin_Returns201AndSetsAvailableCopies()
    {
        var (adminClient, _) = await AuthorizedClientAsync("Admin");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author = new Author { AuthorName = "New Author " + Guid.NewGuid() };
        var category = new Category { CategoryName = "New Cat " + Guid.NewGuid() };
        db.Authors.Add(author);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var payload = new
        {
            title = "Integration Test Book",
            isbn = Guid.NewGuid().ToString("N")[..13],
            authorId = author.AuthorId,
            categoryId = category.CategoryId,
            totalCopies = 5,
            publishedYear = 2024,
        };

        var response = await adminClient.PostAsJsonAsync("/api/books", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // The backend seeds AvailableCopies = TotalCopies on creation
        Assert.Contains("5", body);
    }

    /// <summary>
    /// Getting a book by a non-existent ID returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetBookById_NonExistent_Returns404()
    {
        var (client, _) = await AuthorizedClientAsync("Member");

        var response = await client.GetAsync("/api/books/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Searching with an empty title query returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task SearchBooks_EmptyTitle_ReturnsBadRequest()
    {
        var (client, _) = await AuthorizedClientAsync("Member");

        var response = await client.GetAsync("/api/books/search?title=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("required", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Searching with a whitespace-only title returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task SearchBooks_WhitespaceTitle_ReturnsBadRequest()
    {
        var (client, _) = await AuthorizedClientAsync("Member");

        var response = await client.GetAsync("/api/books/search?title=   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Updating a book to TotalCopies less than currently borrowed copies returns 400.
    /// </summary>
    [Fact]
    public async Task UpdateBook_TotalCopiesBelowBorrowedCount_ReturnsBadRequest()
    {
        var (adminClient, _) = await AuthorizedClientAsync("Admin");
        var (libClient, _) = await AuthorizedClientAsync("Librarian");
        var (_, memberId) = await AuthorizedClientAsync("Member");

        // Book with 3 copies
        var bookId = await SeedBookAsync(totalCopies: 3);

        // Borrow 2 copies (two different members)
        var (_, memberId2) = await AuthorizedClientAsync("Member");
        await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId}", null);
        await libClient.PostAsync(
            $"/api/borrowtransactions/borrow?bookId={bookId}&memberId={memberId2}", null);

        // Retrieve the author/category of the seeded book to construct a valid update payload
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var book = await db.Books.FindAsync(bookId);

        // Try to reduce TotalCopies to 1 — but 2 copies are still borrowed
        var updatePayload = new
        {
            title = book!.Title,
            authorId = book.AuthorId,
            categoryId = book.CategoryId,
            totalCopies = 1,
            publishedYear = book.PublishedYear,
        };

        var response = await adminClient.PutAsJsonAsync($"/api/books/{bookId}", updatePayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("borrowed copies", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Member cannot create a book (403 Forbidden).
    /// </summary>
    [Fact]
    public async Task CreateBook_AsMember_ReturnsForbidden()
    {
        var (memberClient, _) = await AuthorizedClientAsync("Member");

        var payload = new
        {
            title = "Unauthorized Book",
            isbn = "0000000000001",
            authorId = 1,
            categoryId = 1,
            totalCopies = 1,
            publishedYear = 2024,
        };

        var response = await memberClient.PostAsJsonAsync("/api/books", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Unauthenticated request to a protected endpoint returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetBooks_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private response-shape records
    // ──────────────────────────────────────────────────────────────────────

    private sealed record AuthResult(string Token, int UserId);

    private sealed record BorrowResult(TransactionShape Transaction);

    private sealed record TransactionShape(int TransactionId, int BookId, int MemberId, string Status);
}
