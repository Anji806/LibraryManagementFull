using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.UserId)
            .ToListAsync();
    }

    // Tracked: callers modify the user and save it.
    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(user => user.UserId == id);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public void Update(User user)
    {
        _context.MarkModifiedIfDetached(user);
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }

    public async Task DeleteKeepingHistoryAsync(User user)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Snapshot who borrowed before the link to the user is cut.
            await _context.BorrowTransactions
                .Where(t => t.MemberId == user.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.MemberName, user.FullName)
                    .SetProperty(t => t.MemberEmail, user.Email));

            // The FK is ON DELETE SET NULL, so the history rows survive.
            await _context.Users
                .Where(u => u.UserId == user.UserId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
        });
    }

    /// <summary>
    /// BorrowingCount — transactions not yet returned (ReturnDate IS NULL).
    /// ReturnCount    — transactions already returned (ReturnDate IS NOT NULL).
    /// </summary>
    public async Task<(int BorrowingCount, int ReturnCount)>
        GetTransactionCountsAsync(int userId)
    {
        var counts = await _context.BorrowTransactions
            .AsNoTracking()
            .Where(t => t.MemberId == userId)
            .GroupBy(t => t.ReturnDate == null)
            .Select(g => new { NotReturned = g.Key, Count = g.Count() })
            .ToListAsync();

        var borrowing = counts.FirstOrDefault(c => c.NotReturned)?.Count ?? 0;
        var returned = counts.FirstOrDefault(c => !c.NotReturned)?.Count ?? 0;

        return (borrowing, returned);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
