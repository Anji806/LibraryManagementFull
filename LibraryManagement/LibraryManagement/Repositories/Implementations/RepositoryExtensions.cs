using LibraryManagement.Data;
using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

internal static class RepositoryExtensions
{
    /// <summary>
    /// Fills <see cref="Book.CategoryNames"/> (all category names of a book,
    /// comma separated) for every book with a single extra query, instead of
    /// loading the Categories collection onto the entities.
    /// Falls back to the book's primary category name.
    /// </summary>
    public static async Task PopulateCategoryNamesAsync(
        this ApplicationDbContext db,
        IReadOnlyCollection<Book> books)
    {
        if (books.Count == 0)
        {
            return;
        }

        var bookIds = books.Select(book => book.BookId).Distinct().ToList();

        var rows = await (
                from link in db.Set<Dictionary<string, object>>("BookCategories")
                where bookIds.Contains(EF.Property<int>(link, "BookId"))
                join category in db.Categories
                    on EF.Property<int>(link, "CategoryId")
                    equals category.CategoryId
                select new
                {
                    BookId = EF.Property<int>(link, "BookId"),
                    category.CategoryId,
                    category.CategoryName
                })
            .AsNoTracking()
            .ToListAsync();

        var namesByBook = rows
            .GroupBy(row => row.BookId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group
                    .OrderBy(row => row.CategoryId)
                    .Select(row => row.CategoryName)));

        foreach (var book in books)
        {
            book.CategoryNames =
                namesByBook.GetValueOrDefault(book.BookId)
                ?? book.Category?.CategoryName;
        }
    }

    /// <summary>
    /// Makes sure an entity that was not loaded by this context is saved as
    /// an UPDATE of that row (only the entity itself, not its graph).
    /// Entities loaded by the context are change-tracked already.
    /// </summary>
    public static void MarkModifiedIfDetached<T>(
        this ApplicationDbContext db,
        T entity)
        where T : class
    {
        var entry = db.Entry(entity);

        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Modified;
        }
    }

    /// <summary>
    /// PostgreSQL's timestamptz only accepts UTC values. Treat unspecified
    /// values (for example a date parsed from a query string) as UTC.
    /// </summary>
    public static DateTime AsUtc(this DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    public static DateTime? AsUtc(this DateTime? value)
        => value?.AsUtc();
}
