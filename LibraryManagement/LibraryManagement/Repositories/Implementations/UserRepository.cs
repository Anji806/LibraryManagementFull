using Dapper;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using System.Data.Common;

namespace LibraryManagement.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    internal const string SelectUser =
        "SELECT UserId, FullName, Email, PasswordHash, Role, IsActive, Address FROM Users";

    private readonly IDapperUnitOfWork _uow;

    public UserRepository(IDapperUnitOfWork uow)
    {
        _uow = uow;
    }

    internal static async Task<int> InsertAsync(
        DbConnection connection,
        DbTransaction tx,
        User user,
        string lastInsertIdQuery)
    {
        return await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, Address)
              VALUES (@FullName, @Email, @PasswordHash, @Role, @IsActive, @Address); "
            + lastInsertIdQuery,
            user, tx);
    }

    public async Task<List<User>> GetAllAsync()
    {
        var connection = await _uow.GetConnectionAsync();

        var users = await connection.QueryAsync<User>(SelectUser);

        return users.ToList();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        var connection = await _uow.GetConnectionAsync();

        var user = await connection.QueryFirstOrDefaultAsync<User>(
            SelectUser + " WHERE UserId = @id",
            new { id });

        return user == null ? null : _uow.Track(user);
    }

    public async Task AddAsync(User user)
    {
        _uow.Enqueue(async (connection, tx) =>
        {
            user.UserId = await InsertAsync(
                connection, tx, user, _uow.LastInsertIdQuery);
        });

        await Task.CompletedTask;
    }

    public void Update(User user)
    {
        _uow.Enqueue((connection, tx) => connection.ExecuteAsync(
            @"UPDATE Users SET
                FullName = @FullName,
                Email = @Email,
                PasswordHash = @PasswordHash,
                Role = @Role,
                IsActive = @IsActive,
                Address = @Address
              WHERE UserId = @UserId",
            user, tx));
    }

    public void Delete(User user)
    {
        _uow.Enqueue((connection, tx) => connection.ExecuteAsync(
            "DELETE FROM Users WHERE UserId = @UserId",
            new { user.UserId }, tx));
    }

    public async Task DeleteKeepingHistoryAsync(User user)
    {
        await _uow.ExecuteInTransactionAsync(async (connection, tx) =>
        {
            // Snapshot who borrowed before the link to the user is cut.
            await connection.ExecuteAsync(
                @"UPDATE BorrowTransactions
                  SET MemberName = @FullName, MemberEmail = @Email
                  WHERE MemberId = @UserId",
                new { user.FullName, user.Email, user.UserId }, tx);

            // The FK is ON DELETE SET NULL, so the history rows survive.
            await connection.ExecuteAsync(
                "DELETE FROM Users WHERE UserId = @UserId",
                new { user.UserId }, tx);
        });
    }

    /// <summary>
    /// BorrowingCount — transactions not yet returned (ReturnDate IS NULL).
    /// ReturnCount    — transactions already returned (ReturnDate IS NOT NULL).
    /// </summary>
    public async Task<(int BorrowingCount, int ReturnCount)>
        GetTransactionCountsAsync(int userId)
    {
        var connection = await _uow.GetConnectionAsync();

        var borrowingCount = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1) FROM BorrowTransactions
              WHERE MemberId = @userId AND ReturnDate IS NULL",
            new { userId });

        var returnCount = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1) FROM BorrowTransactions
              WHERE MemberId = @userId AND ReturnDate IS NOT NULL",
            new { userId });

        return (borrowingCount, returnCount);
    }

    public async Task SaveChangesAsync()
    {
        await _uow.SaveChangesAsync();
    }
}
