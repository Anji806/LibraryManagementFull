using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Interfaces;
using LibraryManagement.Realtime;

namespace LibraryManagement.Services.Implementations;

public class LoginHistoryService(ILoginHistoryRepository repository, ILoginActivityEventBus eventBus) : ILoginHistoryService
{
    public async Task RecordLoginAsync(LoginHistory entry)
    {
        await repository.InsertLoginHistoryAsync(entry);
        eventBus.PublishLogin(entry);
    }

    public async Task<LoginHistory?> RecordLogoutAsync(string sessionId, DateTime logoutDateTime)
    {
        var updated = await repository.RecordLogoutAsync(sessionId, logoutDateTime);
        if (updated is not null) eventBus.PublishLogout(updated);
        return updated;
    }
    public Task<int> DeleteLoginHistoryAsync(IReadOnlyCollection<long> loginLogIds) => repository.DeleteLoginHistoryAsync(loginLogIds);
    public Task<LoginHistoryPage> GetLoginHistoryAsync(LoginHistoryQuery query) => repository.GetLoginHistoryAsync(query);
    public Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByUserIdAsync(int userId, int page, int pageSize) => repository.GetLoginHistoryByUserIdAsync(userId, page, pageSize);
    public Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByDateRangeAsync(DateTime fromUtc, DateTime toUtc, int page, int pageSize) => repository.GetLoginHistoryByDateRangeAsync(fromUtc, toUtc, page, pageSize);
}
