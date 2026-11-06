using LibraryManagement.Models;

namespace LibraryManagement.Repositories.Interfaces;

public interface IBorrowTransactionRepository
{
    Task<BorrowTransaction?> GetByIdAsync(
        int transactionId);

    Task<BorrowTransaction?> GetActiveBorrowingAsync(
        int bookId,
        int memberId);

    Task<List<BorrowTransaction>>
        GetActiveBorrowingsByMemberAsync(
            int memberId);

    Task<List<BorrowTransaction>>
        GetBorrowHistoryByMemberAsync(
            int memberId);

    Task<List<BorrowTransaction>>
        GetBorrowHistoryByBookAsync(
            int bookId);

    Task AddAsync(
        BorrowTransaction transaction);

    void Update(
        BorrowTransaction transaction);

    Task SaveChangesAsync();
}