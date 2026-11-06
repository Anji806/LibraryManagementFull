using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;

namespace LibraryManagement.Services.Interfaces;

public interface ILoginHistoryService
{
    Task RecordLoginAsync(LoginHistory entry);
    Task<LoginHistory?> RecordLogoutAsync(string sessionId, DateTime logoutDateTime);
    Task<int> DeleteLoginHistoryAsync(IReadOnlyCollection<long> loginLogIds);
    Task<LoginHistoryPage> GetLoginHistoryAsync(LoginHistoryQuery query);
    Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByUserIdAsync(int userId, int page, int pageSize);
    Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByDateRangeAsync(DateTime fromUtc, DateTime toUtc, int page, int pageSize);
}
