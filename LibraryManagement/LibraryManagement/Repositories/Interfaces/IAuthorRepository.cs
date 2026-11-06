using LibraryManagement.Models;

namespace LibraryManagement.Repositories.Interfaces;

public interface IAuthorRepository
{
    Task<List<Author>> GetAllAsync();

    Task<Author?> GetByIdAsync(int id);

    Task AddAsync(Author author);

    void Update(Author author);

    void Delete(Author author);

    Task SaveChangesAsync();
}