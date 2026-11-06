using Dapper;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;

namespace LibraryManagement.Repositories.Implementations;

public class AuthorRepository : IAuthorRepository
{
    private const string BookColumns =
        "BookId, Title, ISBN, AuthorId, CategoryId, TotalCopies, AvailableCopies, PublishedYear";

    private readonly IDapperUnitOfWork _uow;

    public AuthorRepository(IDapperUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<Author>> GetAllAsync()
    {
        var connection = await _uow.GetConnectionAsync();

        var authors = (await connection.QueryAsync<Author>(
            "SELECT AuthorId, AuthorName FROM Authors")).ToList();

        var books = await connection.QueryAsync<Book>(
            $"SELECT {BookColumns} FROM Books");

        var byAuthor = authors.ToDictionary(x => x.AuthorId);

        foreach (var book in books)
        {
            if (byAuthor.TryGetValue(book.AuthorId, out var author))
            {
                author.Books.Add(book);
            }
        }

        return authors;
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        var connection = await _uow.GetConnectionAsync();

        var author = await connection.QueryFirstOrDefaultAsync<Author>(
            "SELECT AuthorId, AuthorName FROM Authors WHERE AuthorId = @id",
            new { id });

        if (author == null)
        {
            return null;
        }

        var books = await connection.QueryAsync<Book>(
            $"SELECT {BookColumns} FROM Books WHERE AuthorId = @id",
            new { id });

        foreach (var book in books)
        {
            author.Books.Add(book);
        }

        return author;
    }

    public async Task AddAsync(Author author)
    {
        _uow.Enqueue(async (connection, tx) =>
        {
            author.AuthorId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Authors (AuthorName) VALUES (@AuthorName); "
                + _uow.LastInsertIdQuery,
                author, tx);
        });

        await Task.CompletedTask;
    }

    public void Update(Author author)
    {
        _uow.Enqueue((connection, tx) => connection.ExecuteAsync(
            "UPDATE Authors SET AuthorName = @AuthorName WHERE AuthorId = @AuthorId",
            author, tx));
    }

    public void Delete(Author author)
    {
        _uow.Enqueue((connection, tx) => connection.ExecuteAsync(
            "DELETE FROM Authors WHERE AuthorId = @AuthorId",
            new { author.AuthorId }, tx));
    }

    public async Task SaveChangesAsync()
    {
        await _uow.SaveChangesAsync();
    }
}
