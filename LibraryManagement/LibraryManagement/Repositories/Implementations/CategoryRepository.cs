using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Read-only list. Books come through the BookCategories join table and
    // carry only the catalogue columns, as the endpoint always has.
    public async Task<List<Category>> GetAllAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.CategoryId)
            .Select(category => new Category
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                Books = category.Books
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

    // Tracked: callers modify or delete the category.
    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _context.Categories
            .Include(category => category.Books)
            .FirstOrDefaultAsync(category => category.CategoryId == id);
    }

    public async Task AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
    }

    public void Update(Category category)
    {
        _context.MarkModifiedIfDetached(category);
    }

    public void Delete(Category category)
    {
        _context.Categories.Remove(category);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
