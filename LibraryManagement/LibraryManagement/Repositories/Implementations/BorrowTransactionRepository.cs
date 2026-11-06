using Dapper;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;

namespace LibraryManagement.Repositories.Implementations;

public class BorrowTransactionRepository
    : IBorrowTransactionRepository
{
    private readonly IDapperUnitOfWork _uow;

    public BorrowTransactionRepository(
        IDapperUnitOfWork uow)
    {
        _uow = uow;
    }

    private async Task<List<BorrowTransaction>> QueryWithRelationsAsync(
        string whereAndOrder,
        object? parameters)
    {
        var connection = await _uow.GetConnectionAsync();

        var transactions = await connection.QueryAsync<
            BorrowTransaction, Book, Author, Category, User?, BorrowTransaction>(
            DapperQueries.TransactionWithRelations + " " + whereAndOrder,
            DapperQueries.MapTransaction,
            parameters,
            splitOn: DapperQueries.TransactionWithRelationsSplitOn);

        return transactions.ToList();
    }

    // Get one transaction by ID
    // Loads:
    // BorrowTransaction -> Book -> Author
    // BorrowTransaction -> Book -> Category
    // BorrowTransaction -> Member
    public async Task<BorrowTransaction?> GetByIdAsync(
        int transactionId)
    {
        var transactions = await QueryWithRelationsAsync(
            "WHERE t.TransactionId = @transactionId",
            new { transactionId });

        var transaction = transactions.FirstOrDefault();

        if (transaction == null)
        {
            return null;
        }

        // Share the Book / Member instances with the other repositories so
        // a change made through one is visible through the other.
        transaction.Book = _uow.Track(transaction.Book);

        if (transaction.Member != null)
        {
            transaction.Member = _uow.Track(transaction.Member);
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
        var connection = await _uow.GetConnectionAsync();

        return await connection.QueryFirstOrDefaultAsync<BorrowTransaction>(
            @"SELECT TransactionId, BookId, MemberId, MemberName, MemberEmail,
                     BorrowDate, ReturnDate, Status
              FROM BorrowTransactions
              WHERE BookId = @bookId
                AND MemberId = @memberId
                AND Status = 'Borrowed'",
            new { bookId, memberId });
    }

    // Get currently borrowed books for a member
    // Loads:
    // BorrowTransaction -> Book -> Author
    // BorrowTransaction -> Book -> Category
    // BorrowTransaction -> Member
    public async Task<List<BorrowTransaction>>
        GetActiveBorrowingsByMemberAsync(
            int memberId)
    {
        return await QueryWithRelationsAsync(
            "WHERE t.MemberId = @memberId AND t.Status = 'Borrowed'",
            new { memberId });
    }

    // Get complete borrowing history for a member
    public async Task<List<BorrowTransaction>>
        GetBorrowHistoryByMemberAsync(
            int memberId)
    {
        return await QueryWithRelationsAsync(
            "WHERE t.MemberId = @memberId ORDER BY t.BorrowDate DESC",
            new { memberId });
    }

    // Get complete borrowing history for a book
    public async Task<List<BorrowTransaction>>
        GetBorrowHistoryByBookAsync(
            int bookId)
    {
        return await QueryWithRelationsAsync(
            "WHERE t.BookId = @bookId ORDER BY t.BorrowDate DESC",
            new { bookId });
    }

    // Add a new borrowing transaction
    public async Task AddAsync(
        BorrowTransaction transaction)
    {
        // Same relationship fix-up EF did for tracked Book / Member.
        transaction.Book = _uow.FindTrackedBook(transaction.BookId)!;

        if (transaction.MemberId.HasValue)
        {
            transaction.Member = _uow.FindTrackedUser(transaction.MemberId.Value);
        }

        _uow.Enqueue(async (connection, tx) =>
        {
            transaction.TransactionId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO BorrowTransactions
                    (BookId, MemberId, MemberName, MemberEmail, BorrowDate, ReturnDate, Status)
                  VALUES
                    (@BookId, @MemberId, @MemberName, @MemberEmail, @BorrowDate, @ReturnDate, @Status); "
                + _uow.LastInsertIdQuery,
                transaction, tx);
        });

        await Task.CompletedTask;
    }

    // Update an existing borrowing transaction
    public void Update(
        BorrowTransaction transaction)
    {
        _uow.Enqueue((connection, tx) => connection.ExecuteAsync(
            @"UPDATE BorrowTransactions SET
                BookId = @BookId,
                MemberId = @MemberId,
                MemberName = @MemberName,
                MemberEmail = @MemberEmail,
                BorrowDate = @BorrowDate,
                ReturnDate = @ReturnDate,
                Status = @Status
              WHERE TransactionId = @TransactionId",
            transaction, tx));
    }

    // Save changes
    public async Task SaveChangesAsync()
    {
        await _uow.SaveChangesAsync();
    }
}
