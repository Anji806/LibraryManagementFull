using Dapper;
using LibraryManagement.Models;

namespace LibraryManagement.Repositories.Implementations;

/// <summary>
/// SQL fragments and mappers shared by the Dapper repositories.
/// </summary>
internal static class DapperQueries
{
    // Book -> Author, Category
    public const string BookWithRelations = @"
SELECT b.BookId, b.Title, b.ISBN, b.AuthorId, b.CategoryId,
       b.TotalCopies, b.AvailableCopies, b.PublishedYear,
       b.PreviewIntroduction, b.PreviewTheme, b.PreviewAbstract, b.PreviewMotto, b.PreviewWhyRead, b.CoverUrl,
       (SELECT group_concat(c2.CategoryName, ', ') FROM BookCategories bc2 INNER JOIN Categories c2 ON c2.CategoryId = bc2.CategoryId WHERE bc2.BookId = b.BookId) AS CategoryNames,
       a.AuthorId, a.AuthorName,
       c.CategoryId, c.CategoryName
FROM Books b
INNER JOIN Authors a ON a.AuthorId = b.AuthorId
INNER JOIN Categories c ON c.CategoryId = b.CategoryId";

    public const string BookWithRelationsSplitOn = "AuthorId,CategoryId";

    public static Book MapBook(Book book, Author author, Category category)
    {
        book.Author = author;
        book.Category = category;
        book.CategoryNames ??= category.CategoryName;
        return book;
    }

    // BorrowTransaction -> Book -> Author, Category ; BorrowTransaction -> Member
    public const string TransactionWithRelations = @"
SELECT t.TransactionId, t.BookId, t.MemberId, t.MemberName, t.MemberEmail,
       t.BorrowDate, t.ReturnDate, t.Status,
       b.BookId, b.Title, b.ISBN, b.AuthorId, b.CategoryId,
       b.TotalCopies, b.AvailableCopies, b.PublishedYear,
       (SELECT group_concat(c2.CategoryName, ', ') FROM BookCategories bc2 INNER JOIN Categories c2 ON c2.CategoryId = bc2.CategoryId WHERE bc2.BookId = b.BookId) AS CategoryNames,
       a.AuthorId, a.AuthorName,
       c.CategoryId, c.CategoryName,
       u.UserId, u.FullName, u.Email, u.PasswordHash, u.Role, u.IsActive, u.Address
FROM BorrowTransactions t
INNER JOIN Books b ON b.BookId = t.BookId
INNER JOIN Authors a ON a.AuthorId = b.AuthorId
INNER JOIN Categories c ON c.CategoryId = b.CategoryId
LEFT JOIN Users u ON u.UserId = t.MemberId";

    public const string TransactionWithRelationsSplitOn
        = "BookId,AuthorId,CategoryId,UserId";

    public static BorrowTransaction MapTransaction(
        BorrowTransaction transaction,
        Book book,
        Author author,
        Category category,
        User? member)
    {
        book.Author = author;
        book.Category = category;
        book.CategoryNames ??= category.CategoryName;
        transaction.Book = book;
        transaction.Member = member;
        return transaction;
    }

    /// <summary>
    /// Escapes LIKE wildcards so the search text is matched literally,
    /// like EF's string.Contains. Use with ESCAPE '\'.
    /// </summary>
    public static string ContainsPattern(string text)
        => "%" + text
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[") + "%";
}
