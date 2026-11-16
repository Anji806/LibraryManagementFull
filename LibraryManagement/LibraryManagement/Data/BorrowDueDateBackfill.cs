using LibraryManagement.Services;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Data;

/// <summary>
/// Gives loans made before due dates existed a due date: borrow date plus the
/// loan period, calculated by exactly the same rule as new loans. Only rows
/// whose DueDate is empty are touched, nothing else about them changes, and no
/// fine is stored (returned loans keep a fine of 0; open loans are fined only
/// from <see cref="BorrowingOptions.FinesStartDate"/> onward). Idempotent.
/// </summary>
public static class BorrowDueDateBackfill
{
    public static async Task<int> RunAsync(ApplicationDbContext db, BorrowingPolicy policy)
    {
        var pending = await db.BorrowTransactions
            .Where(t => t.DueDate == null)
            .ToListAsync();

        foreach (var transaction in pending)
        {
            transaction.DueDate = policy.ComputeDueDate(transaction.BorrowDate);
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        return pending.Count;
    }
}
