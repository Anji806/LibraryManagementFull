using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

public class BorrowTransactionRepository
    : IBorrowTransactionRepository
{
    private readonly ApplicationDbContext _context;
    private readonly BorrowingPolicy _policy;

    public BorrowTransactionRepository(
        ApplicationDbContext context,
        BorrowingPolicy policy)
    {
        _context = context;
        _policy = policy;
    }

    // BorrowTransaction -> Book -> Author
    // BorrowTransaction -> Book -> Category
    // BorrowTransaction -> Member
    private IQueryable<BorrowTransaction> TransactionsWithRelations()
        => _context.BorrowTransactions
            .Include(t => t.Book).ThenInclude(book => book.Author)
            .Include(t => t.Book).ThenInclude(book => book.Category)
            .Include(t => t.Member);

    private async Task<List<BorrowTransaction>> ToListWithCategoryNamesAsync(
        IQueryable<BorrowTransaction> query)
    {
        var transactions = await query.ToListAsync();

        await _context.PopulateCategoryNamesAsync(
            transactions.Select(t => t.Book).ToList());

        // Overdue status and fines are always calculated here, never trusted
        // from the client or from a stale stored value.
        var now = DateTime.UtcNow;
        foreach (var transaction in transactions)
        {
            _policy.Apply(transaction, now);
        }

        return transactions;
    }

    // Get one transaction by ID. Tracked: the return flow updates it and
    // shares the Book / Member instances with the other repositories.
    public async Task<BorrowTransaction?> GetByIdAsync(
        int transactionId)
    {
        var transaction = await TransactionsWithRelations()
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (transaction != null)
        {
            await _context.PopulateCategoryNamesAsync([transaction.Book]);
            _policy.Apply(transaction, DateTime.UtcNow);
        }

        return transaction;
    }

    // Check whether a member already has
    // an active borrowing for a particular book
    public async Task<BorrowTransaction?>
        GetActiveBorrowingAsync(
            int bookId,
            int memberId)
    {
        return await _context.BorrowTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.BookId == bookId &&
                t.MemberId == memberId &&
                t.Status == "Borrowed");
    }

    // Get currently borrowed books for a member
    public async Task<List<BorrowTransaction>>
        GetActiveBorrowingsByMemberAsync(
            int memberId)
    {
        return await ToListWithCategoryNamesAsync(
            TransactionsWithRelations()
                .AsNoTracking()
                .Where(t => t.MemberId == memberId && t.Status == "Borrowed")
                .OrderBy(t => t.TransactionId));
    }

    // Get complete borrowing history for a member
    public async Task<List<BorrowTransaction>>
        GetBorrowHistoryByMemberAsync(
            int memberId)
    {
        return await ToListWithCategoryNamesAsync(
            TransactionsWithRelations()
                .AsNoTracking()
                .Where(t => t.MemberId == memberId)
                .OrderByDescending(t => t.BorrowDate)
                .ThenBy(t => t.TransactionId));
    }

    // Get complete borrowing history for a book
    public async Task<List<BorrowTransaction>>
        GetBorrowHistoryByBookAsync(
            int bookId)
    {
        return await ToListWithCategoryNamesAsync(
            TransactionsWithRelations()
                .AsNoTracking()
                .Where(t => t.BookId == bookId)
                .OrderByDescending(t => t.BorrowDate)
                .ThenBy(t => t.TransactionId));
    }

    // Add a new borrowing transaction
    public async Task AddAsync(
        BorrowTransaction transaction)
    {
        transaction.BorrowDate = transaction.BorrowDate.AsUtc();
        transaction.ReturnDate = transaction.ReturnDate.AsUtc();

        await _context.BorrowTransactions.AddAsync(transaction);
    }

    // Update an existing borrowing transaction
    public void Update(
        BorrowTransaction transaction)
    {
        transaction.BorrowDate = transaction.BorrowDate.AsUtc();
        transaction.ReturnDate = transaction.ReturnDate.AsUtc();

        _context.MarkModifiedIfDetached(transaction);
    }

    // Saves every pending change of the request (book copies and the
    // transaction) in one database transaction.
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
