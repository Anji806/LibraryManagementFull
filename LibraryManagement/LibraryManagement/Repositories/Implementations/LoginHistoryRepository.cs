using LibraryManagement.Data;
using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Repositories.Implementations;

public class LoginHistoryRepository(ApplicationDbContext context) : ILoginHistoryRepository
{
    public async Task InsertLoginHistoryAsync(LoginHistory entry)
    {
        entry.LoginDateTime = entry.LoginDateTime.AsUtc();
        entry.LogoutDateTime = entry.LogoutDateTime.AsUtc();
        entry.SessionExpiresAt = entry.SessionExpiresAt.AsUtc();

        context.LoginHistory.Add(entry);
        await context.SaveChangesAsync();
    }

    public async Task<LoginHistory?> RecordLogoutAsync(string sessionId, DateTime logoutDateTime)
    {
        logoutDateTime = logoutDateTime.AsUtc();

        IQueryable<LoginHistory> activeSession = context.LoginHistory.Where(x =>
            x.SessionId == sessionId &&
            x.LoginStatus == "Success" &&
            x.SessionStatus == "Active");

        var item = await activeSession.AsNoTracking().SingleOrDefaultAsync();
        if (item is null)
        {
            return null;
        }

        // Conditional UPDATE: only one concurrent logout can win.
        var updated = await activeSession.ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.LogoutDateTime, logoutDateTime)
            .SetProperty(x => x.SessionStatus, "Logged Out"));
        if (updated != 1)
        {
            return null;
        }

        item.LogoutDateTime = logoutDateTime;
        item.SessionStatus = "Logged Out";
        return item;
    }

    public async Task<int> DeleteLoginHistoryAsync(IReadOnlyCollection<long> loginLogIds)
    {
        var ids = loginLogIds.ToArray();
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            // All-or-nothing: a stale selection deletes nothing.
            var existingCount = await context.LoginHistory
                .CountAsync(x => ids.Contains(x.LoginLogId));
            if (existingCount != ids.Length)
            {
                await transaction.RollbackAsync();
                return -1;
            }

            var deletedCount = await context.LoginHistory
                .Where(x => ids.Contains(x.LoginLogId))
                .ExecuteDeleteAsync();
            await transaction.CommitAsync();
            return deletedCount;
        });
    }

    public async Task<LoginHistoryPage> GetLoginHistoryAsync(LoginHistoryQuery query)
    {
        var now = DateTime.UtcNow;
        await context.LoginHistory
            .Where(x =>
                x.LoginStatus == "Success" &&
                x.SessionStatus == "Active" &&
                x.SessionExpiresAt != null &&
                x.SessionExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SessionStatus, "Expired"));

        var entries = context.LoginHistory.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            var username = query.Username.Trim().ToLower();
            entries = entries.Where(x => x.Username.ToLower().Contains(username));
        }

        if (query.Status is not null)
        {
            entries = entries.Where(x => x.LoginStatus == query.Status);
        }

        if (query.FromUtc is { } fromUtc)
        {
            var from = fromUtc.AsUtc();
            entries = entries.Where(x => x.LoginDateTime >= from);
        }

        if (query.ToUtc is { } toUtc)
        {
            var to = toUtc.AsUtc();
            entries = entries.Where(x => x.LoginDateTime < to);
        }

        var total = await entries.CountAsync();

        var ordered = query.SortDirection == "asc"
            ? entries.OrderBy(x => x.LoginDateTime)
            : entries.OrderByDescending(x => x.LoginDateTime);

        var items = await ordered
            .ThenByDescending(x => x.LoginLogId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new LoginHistoryPage { Items = items, TotalCount = total, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByUserIdAsync(int userId, int page, int pageSize)
        => await QueryPage(context.LoginHistory.Where(x => x.UserId == userId), page, pageSize);

    public async Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByDateRangeAsync(DateTime fromUtc, DateTime toUtc, int page, int pageSize)
    {
        var from = fromUtc.AsUtc();
        var to = toUtc.AsUtc();
        return await QueryPage(
            context.LoginHistory.Where(x => x.LoginDateTime >= from && x.LoginDateTime < to),
            page,
            pageSize);
    }

    private static async Task<IReadOnlyList<LoginHistory>> QueryPage(IQueryable<LoginHistory> entries, int page, int pageSize)
        => await entries
            .AsNoTracking()
            .OrderByDescending(x => x.LoginDateTime)
            .ThenByDescending(x => x.LoginLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
}
