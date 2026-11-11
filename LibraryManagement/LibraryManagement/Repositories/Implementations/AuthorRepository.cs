using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

public class AuthorRepository : IAuthorRepository
{
    private readonly ApplicationDbContext _context;

    public AuthorRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Read-only list. The nested books carry only the catalogue columns
    // (not the long preview texts), as the endpoint always has.
    public async Task<List<Author>> GetAllAsync()
    {
        return await _context.Authors
            .AsNoTracking()
            .OrderBy(author => author.AuthorId)
            .Select(author => new Author
            {
                AuthorId = author.AuthorId,
                AuthorName = author.AuthorName,
                Books = author.Books
                    .OrderBy(book => book.BookId)
                    .Select(book => new Book
                    {
                        BookId = book.BookId,
                        Title = book.Title,
                        ISBN = book.ISBN,
                        AuthorId = book.AuthorId,
                        CategoryId = book.CategoryId,
                        TotalCopies = book.TotalCopies,
                        AvailableCopies = book.AvailableCopies,
                        PublishedYear = book.PublishedYear
                    })
                    .ToList()
            })
            .ToListAsync();
    }

    // Tracked: callers modify or delete the author.
    public async Task<Author?> GetByIdAsync(int id)
    {
        return await _context.Authors
            .Include(author => author.Books)
            .FirstOrDefaultAsync(author => author.AuthorId == id);
    }

    public async Task AddAsync(Author author)
    {
        await _context.Authors.AddAsync(author);
    }

    public void Update(Author author)
    {
        _context.MarkModifiedIfDetached(author);
    }

    public void Delete(Author author)
    {
        _context.Authors.Remove(author);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
