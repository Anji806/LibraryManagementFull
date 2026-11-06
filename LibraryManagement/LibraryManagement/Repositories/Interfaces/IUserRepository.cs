using LibraryManagement.Models;

namespace LibraryManagement.Repositories.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();

    Task<User?> GetByIdAsync(int id);

    Task AddAsync(User user);

    void Update(User user);

    void Delete(User user);

    /// <summary>
    /// Deletes the user but keeps their borrow history: the user's name and
    /// email are copied onto their transactions and MemberId is set to null.
    /// </summary>
    Task DeleteKeepingHistoryAsync(User user);

    /// <summary>
    /// Returns the number of not-yet-returned transactions (ReturnDate IS NULL)
    /// and the number of returned transactions (ReturnDate IS NOT NULL).
    /// </summary>
    Task<(int BorrowingCount, int ReturnCount)> GetTransactionCountsAsync(int userId);

    Task SaveChangesAsync();
}