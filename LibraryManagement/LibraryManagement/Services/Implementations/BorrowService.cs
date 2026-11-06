using LibraryManagement.Exceptions;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Interfaces;

namespace LibraryManagement.Services.Implementations;

public class BorrowService : IBorrowService
{
    private readonly IBookRepository _bookRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBorrowTransactionRepository
        _borrowRepository;

    public BorrowService(
        IBookRepository bookRepository,
        IUserRepository userRepository,
        IBorrowTransactionRepository borrowRepository)
    {
        _bookRepository = bookRepository;
        _userRepository = userRepository;
        _borrowRepository = borrowRepository;
    }

    public async Task<BorrowTransaction> BorrowAsync(
        int bookId,
        int memberId)
    {
        // 1. Check book
        var book =
            await _bookRepository.GetByIdAsync(bookId);

        if (book == null)
        {
            throw new NotFoundException(
                "Book not found.");
        }

        // 2. Check member
        var member =
            await _userRepository.GetByIdAsync(memberId);

        if (member == null)
        {
            throw new NotFoundException(
                "Member not found.");
        }

        if (!member.IsActive)
        {
            throw new BusinessRuleException(
                "Member account is inactive.");
        }

        if (member.Role != "Member")
        {
            throw new BusinessRuleException(
                "Only members can borrow books.");
        }

        // 3. Check available copies
        if (book.AvailableCopies <= 0)
        {
            throw new BusinessRuleException(
                "No available copies of this book.");
        }

        // 4. Check duplicate active borrowing
        var existingBorrow =
            await _borrowRepository
                .GetActiveBorrowingAsync(
                    bookId,
                    memberId);

        if (existingBorrow != null)
        {
            throw new BusinessRuleException(
                "This member has already borrowed this book.");
        }

        // 5. Decrease available copies
        book.AvailableCopies--;

        _bookRepository.Update(book);

        // 6. Create transaction
        var transaction = new BorrowTransaction
        {
            BookId = bookId,
            MemberId = memberId,
            BorrowDate = DateTime.UtcNow,
            ReturnDate = null,
            Status = "Borrowed"
        };

        await _borrowRepository.AddAsync(transaction);

        // 7. Save changes
        await _borrowRepository.SaveChangesAsync();

        return transaction;
    }
    

    public async Task<BorrowTransaction> ReturnAsync(
        int transactionId)
    {
        // 1. Find transaction
        var transaction =
            await _borrowRepository
                .GetByIdAsync(transactionId);

        if (transaction == null)
        {
            throw new NotFoundException(
                "Borrow transaction not found.");
        }

        // 2. Check already returned
        if (transaction.Status == "Returned" ||
            transaction.ReturnDate.HasValue)
        {
            throw new BusinessRuleException(
                "This book has already been returned.");
        }

        // 3. Find book
        var book =
            await _bookRepository
                .GetByIdAsync(transaction.BookId);

        if (book == null)
        {
            throw new NotFoundException(
                "Book not found.");
        }

        // 4. Mark transaction returned
        transaction.ReturnDate = DateTime.UtcNow;
        transaction.Status = "Returned";

        // 5. Increase available copies
        if (book.AvailableCopies < book.TotalCopies)
        {
            book.AvailableCopies++;
        }

        _borrowRepository.Update(transaction);
        _bookRepository.Update(book);

        // 6. Save
        await _borrowRepository.SaveChangesAsync();

        return transaction;
    }
}