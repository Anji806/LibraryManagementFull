using LibraryManagement.Models;

namespace LibraryManagement.Services.Interfaces;

public interface IBorrowService
{
    Task<BorrowTransaction> BorrowAsync(
        int bookId,
        int memberId);

    Task<BorrowTransaction> ReturnAsync(
        int transactionId);
}