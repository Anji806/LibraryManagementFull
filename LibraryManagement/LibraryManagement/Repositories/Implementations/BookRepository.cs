using Dapper;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;

namespace LibraryManagement.Repositories.Implementations;

public class BookRepository : IBookRepository
{
    private readonly IDapperUnitOfWork _uow;

    public BookRepository(IDapperUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<Book>> GetAllAsync()
    {
        var connection = await _uow.GetConnectionAsync();

        var books = await connection.QueryAsync<Book, Author, Category, Book>(
            DapperQueries.BookWithRelations,
            DapperQueries.MapBook,
            splitOn: DapperQueries.BookWithRelationsSplitOn);

        return books.ToList();
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        var connection = await _uow.GetConnectionAsync();

        var books = await connection.QueryAsync<Book, Author, Category, Book>(
            DapperQueries.BookWithRelations + " WHERE b.BookId = @id",
            DapperQueries.MapBook,
            new { id },
            splitOn: DapperQueries.BookWithRelationsSplitOn);

        var book = books.FirstOrDefault();

        return book == null ? null : _uow.Track(book);
    }

    public async Task<List<Book>> SearchAsync(string title)
    {
        var connection = await _uow.GetConnectionAsync();

        var books = await connection.QueryAsync<Book, Author, Category, Book>(
            DapperQueries.BookWithRelations
                + @" WHERE b.Title LIKE @pattern ESCAPE '\'",
            DapperQueries.MapBook,
            new { pattern = DapperQueries.ContainsPattern(title) },
            splitOn: DapperQueries.BookWithRelationsSplitOn);

        return books.ToList();
    }

    public async Task AddAsync(Book book)
    {
        _uow.Enqueue(async (connection, tx) =>
        {
            book.BookId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Books
                    (Title, ISBN, AuthorId, CategoryId, TotalCopies, AvailableCopies, PublishedYear,
                     PreviewIntroduction, PreviewTheme, PreviewAbstract, PreviewMotto, PreviewWhyRead, CoverUrl)
                  VALUES
                    (@Title, @ISBN, @AuthorId, @CategoryId, @TotalCopies, @AvailableCopies, @PublishedYear,
                     @PreviewIntroduction, @PreviewTheme, @PreviewAbstract, @PreviewMotto, @PreviewWhyRead, @CoverUrl); "
                + _uow.LastInsertIdQuery,
                book, tx);
            await connection.ExecuteAsync(
                "INSERT OR IGNORE INTO BookCategories (BookId, CategoryId) VALUES (@BookId, @CategoryId)",
                new { book.BookId, book.CategoryId }, tx);
        });

        await Task.CompletedTask;
    }

    public void Update(Book book)
    {
        _uow.Enqueue(async (connection, tx) =>
        {
            await connection.ExecuteAsync(@"UPDATE Books SET
                Title = @Title,
                ISBN = @ISBN,
                AuthorId = @AuthorId,
                CategoryId = @CategoryId,
                TotalCopies = @TotalCopies,
                AvailableCopies = @AvailableCopies,
                PublishedYear = @PublishedYear,
                PreviewIntroduction = @PreviewIntroduction,
                PreviewTheme = @PreviewTheme,
                PreviewAbstract = @PreviewAbstract,
                PreviewMotto = @PreviewMotto,
                PreviewWhyRead = @PreviewWhyRead,
                CoverUrl = @CoverUrl
              WHERE BookId = @BookId",
            book, tx);
            await connection.ExecuteAsync(
                "INSERT OR IGNORE INTO BookCategories (BookId, CategoryId) VALUES (@BookId, @CategoryId)",
                new { book.BookId, book.CategoryId }, tx);
        });
    }

    public void Delete(Book book)
    {
        _uow.Enqueue((connection, tx) => connection.ExecuteAsync(
            "DELETE FROM Books WHERE BookId = @BookId",
            new { book.BookId }, tx));
    }

    public async Task<bool> HasBorrowHistoryAsync(int bookId)
    {
        var connection = await _uow.GetConnectionAsync();

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM BorrowTransactions WHERE BookId = @bookId",
            new { bookId });

        return count > 0;
    }

    public async Task<bool> ExistsAsync(int bookId)
    {
        var connection = await _uow.GetConnectionAsync();

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Books WHERE BookId = @bookId",
            new { bookId });

        return count > 0;
    }

    public async Task SaveChangesAsync()
    {
        await _uow.SaveChangesAsync();
    }
}
