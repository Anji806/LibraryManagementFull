using LibraryManagement.Models;

namespace LibraryManagement.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email);


    Task<User?> GetByIdAsync(int userId);

    Task AddAsync(User user);

    Task SaveChangesAsync();
}
