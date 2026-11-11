using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

public class BookRepository : IBookRepository
{
    private const string BookCategoriesTable = "BookCategories";

    private readonly ApplicationDbContext _context;

    // Books whose primary category must be present in BookCategories when
    // the changes are saved.
    private readonly List<Book> _booksToLink = new();

    public BookRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<Book> BooksWithRelations()
        => _context.Books
            .Include(book => book.Author)
            .Include(book => book.Category);

    public async Task<List<Book>> GetAllAsync()
    {
        var books = await BooksWithRelations()
            .AsNoTracking()
            .OrderBy(book => book.BookId)
            .ToListAsync();

        await _context.PopulateCategoryNamesAsync(books);

        return books;
    }

    // Tracked: callers modify, borrow against or delete the book.
    public async Task<Book?> GetByIdAsync(int id)
    {
        var book = await BooksWithRelations()
            .FirstOrDefaultAsync(book => book.BookId == id);

        if (book != null)
        {
            await _context.PopulateCategoryNamesAsync([book]);
        }

        return book;
    }

    // Case-insensitive "contains" on the title.
    public async Task<List<Book>> SearchAsync(string title)
    {
        var term = title.ToLower();

        var books = await BooksWithRelations()
            .AsNoTracking()
            .Where(book => book.Title.ToLower().Contains(term))
            .OrderBy(book => book.BookId)
            .ToListAsync();

        await _context.PopulateCategoryNamesAsync(books);

        return books;
    }

    public async Task AddAsync(Book book)
    {
        await _context.Books.AddAsync(book);
        _booksToLink.Add(book);
    }

    public void Update(Book book)
    {
        _context.MarkModifiedIfDetached(book);
        _booksToLink.Add(book);
    }

    public void Delete(Book book)
    {
        // BookCategories rows go with the book (ON DELETE CASCADE).
        _context.Books.Remove(book);
    }

    public async Task<bool> HasBorrowHistoryAsync(int bookId)
    {
        return await _context.BorrowTransactions
            .AnyAsync(t => t.BookId == bookId);
    }

    public async Task<bool> ExistsAsync(int bookId)
    {
        return await _context.Books.AnyAsync(book => book.BookId == bookId);
    }

    public async Task SaveChangesAsync()
    {
        var booksToLink = _booksToLink.ToList();
        _booksToLink.Clear();

        if (booksToLink.Count == 0)
        {
            await _context.SaveChangesAsync();
            return;
        }

        // The book row and its BookCategories link are saved together.
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Needed first so a new book has its generated BookId.
            await _context.SaveChangesAsync();

            var links = _context.Set<Dictionary<string, object>>(
                BookCategoriesTable);

            foreach (var book in booksToLink)
            {
                var linked = await links.AnyAsync(link =>
                    EF.Property<int>(link, "BookId") == book.BookId &&
                    EF.Property<int>(link, "CategoryId") == book.CategoryId);

                if (!linked)
                {
                    links.Add(new Dictionary<string, object>
                    {
                        ["BookId"] = book.BookId,
                        ["CategoryId"] = book.CategoryId
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }
}
