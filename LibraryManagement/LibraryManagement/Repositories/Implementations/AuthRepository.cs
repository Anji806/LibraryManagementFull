using Dapper;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;

namespace LibraryManagement.Repositories.Implementations;

public class AuthRepository : IAuthRepository
{
    private readonly IDapperUnitOfWork _uow;

    public AuthRepository(IDapperUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var connection = await _uow.GetConnectionAsync();

        var user = await connection.QueryFirstOrDefaultAsync<User>(
            UserRepository.SelectUser + " WHERE Email = @email COLLATE NOCASE",
            new { email });

        return user == null ? null : _uow.Track(user);
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        var connection = await _uow.GetConnectionAsync();

        var user = await connection.QueryFirstOrDefaultAsync<User>(
            UserRepository.SelectUser + " WHERE UserId = @userId",
            new { userId });

        return user == null ? null : _uow.Track(user);
    }

    public async Task AddAsync(User user)
    {
        _uow.Enqueue(async (connection, tx) =>
        {
            user.UserId = await UserRepository.InsertAsync(
                connection, tx, user, _uow.LastInsertIdQuery);
        });

        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _uow.SaveChangesAsync();
    }
}
