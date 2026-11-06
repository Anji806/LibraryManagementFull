using LibraryManagement.Models;

namespace LibraryManagement.Repositories.Interfaces;

public interface IBookRepository
{
    Task<List<Book>> GetAllAsync();

    Task<Book?> GetByIdAsync(int id);

    Task<List<Book>> SearchAsync(string title);

    Task AddAsync(Book book);

    void Update(Book book);

    void Delete(Book book);

    Task<bool> HasBorrowHistoryAsync(int bookId);

    Task<bool> ExistsAsync(int bookId);

    Task SaveChangesAsync();
}