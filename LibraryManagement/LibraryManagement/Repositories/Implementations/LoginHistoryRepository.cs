using Dapper;
using LibraryManagement.Data;
using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;

namespace LibraryManagement.Repositories.Implementations;

public class LoginHistoryRepository(IDapperUnitOfWork uow) : ILoginHistoryRepository
{
    private const string Columns = "LoginLogId, UserId, Username, LoginDateTime, LogoutDateTime, SessionExpiresAt, SessionId, IPAddress, Browser, OperatingSystem, LoginStatus, FailureReason, SessionStatus";

    public async Task InsertLoginHistoryAsync(LoginHistory entry)
    {
        var connection = await uow.GetConnectionAsync();
        entry.LoginLogId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO LoginHistory (UserId, Username, LoginDateTime, LogoutDateTime, SessionExpiresAt, SessionId, IPAddress, Browser, OperatingSystem, LoginStatus, FailureReason, SessionStatus)
            VALUES (@UserId, @Username, @LoginDateTime, @LogoutDateTime, @SessionExpiresAt, @SessionId, @IPAddress, @Browser, @OperatingSystem, @LoginStatus, @FailureReason, @SessionStatus);
            SELECT last_insert_rowid();
            """, entry);
    }

    public async Task<LoginHistory?> RecordLogoutAsync(string sessionId, DateTime logoutDateTime)
    {
        var connection = await uow.GetConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var item = await connection.QuerySingleOrDefaultAsync<LoginHistory>(
            $"SELECT {Columns} FROM LoginHistory WHERE SessionId = @sessionId AND LoginStatus = 'Success' AND SessionStatus = 'Active'",
            new { sessionId }, transaction);
        if (item is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        var updated = await connection.ExecuteAsync(
            "UPDATE LoginHistory SET LogoutDateTime = @logoutDateTime, SessionStatus = 'Logged Out' WHERE SessionId = @sessionId AND LoginStatus = 'Success' AND SessionStatus = 'Active'",
            new { sessionId, logoutDateTime }, transaction);
        if (updated != 1)
        {
            await transaction.RollbackAsync();
            return null;
        }

        await transaction.CommitAsync();
        item.LogoutDateTime = logoutDateTime;
        item.SessionStatus = "Logged Out";
        return item;
    }

    public async Task<int> DeleteLoginHistoryAsync(IReadOnlyCollection<long> loginLogIds)
    {
        var connection = await uow.GetConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var ids = new { Ids = loginLogIds };
            var existingCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM LoginHistory WHERE LoginLogId IN @Ids", ids, transaction);
            if (existingCount != loginLogIds.Count)
            {
                await transaction.RollbackAsync();
                return -1;
            }

            var deletedCount = await connection.ExecuteAsync(
                "DELETE FROM LoginHistory WHERE LoginLogId IN @Ids", ids, transaction);
            await transaction.CommitAsync();
            return deletedCount;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<LoginHistoryPage> GetLoginHistoryAsync(LoginHistoryQuery query)
    {
        var connection = await uow.GetConnectionAsync();
        await connection.ExecuteAsync("UPDATE LoginHistory SET SessionStatus = 'Expired' WHERE LoginStatus = 'Success' AND SessionStatus = 'Active' AND SessionExpiresAt IS NOT NULL AND SessionExpiresAt <= @now", new { now = DateTime.UtcNow });
        var where = "WHERE (@Username IS NULL OR Username LIKE '%' || @Username || '%') AND (@Status IS NULL OR LoginStatus = @Status) AND (@FromUtc IS NULL OR LoginDateTime >= @FromUtc) AND (@ToUtc IS NULL OR LoginDateTime < @ToUtc)";
        var parameters = new
        {
            Username = string.IsNullOrWhiteSpace(query.Username) ? null : query.Username.Trim(),
            Status = query.Status,
            query.FromUtc,
            query.ToUtc,
            Offset = (query.Page - 1) * query.PageSize,
            query.PageSize
        };
        var total = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM LoginHistory {where}", parameters);
        var items = await connection.QueryAsync<LoginHistory>($"SELECT {Columns} FROM LoginHistory {where} ORDER BY LoginDateTime {(query.SortDirection == "asc" ? "ASC" : "DESC")}, LoginLogId DESC LIMIT @PageSize OFFSET @Offset", parameters);
        return new LoginHistoryPage { Items = items.AsList(), TotalCount = total, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByUserIdAsync(int userId, int page, int pageSize)
        => await QueryPage("UserId = @userId", new { userId }, page, pageSize);

    public async Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByDateRangeAsync(DateTime fromUtc, DateTime toUtc, int page, int pageSize)
        => await QueryPage("LoginDateTime >= @fromUtc AND LoginDateTime < @toUtc", new { fromUtc, toUtc }, page, pageSize);

    private async Task<IReadOnlyList<LoginHistory>> QueryPage(string predicate, object parameters, int page, int pageSize)
    {
        var connection = await uow.GetConnectionAsync();
        var values = new DynamicParameters(parameters);
        values.Add("Offset", (page - 1) * pageSize);
        values.Add("PageSize", pageSize);
        var rows = await connection.QueryAsync<LoginHistory>($"SELECT {Columns} FROM LoginHistory WHERE {predicate} ORDER BY LoginDateTime DESC, LoginLogId DESC LIMIT @PageSize OFFSET @Offset", values);
        return rows.AsList();
    }
}
