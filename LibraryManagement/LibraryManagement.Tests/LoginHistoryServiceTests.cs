using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Implementations;
using LibraryManagement.Realtime;

namespace LibraryManagement.Tests;

public class LoginHistoryServiceTests
{
    [Fact]
    public async Task RecordLoginAsync_ForwardsAuditRecordWithoutAddingSecrets()
    {
        var repository = new FakeLoginHistoryRepository();
        var bus = new LoginActivityEventBus();
        using var subscription = bus.Subscribe();
        var service = new LoginHistoryService(repository, bus);
        var record = new LoginHistory { Username = "reader@example.com", LoginStatus = "Failed", FailureReason = "Invalid credentials." };

        await service.RecordLoginAsync(record);

        Assert.Same(record, repository.Inserted);
        Assert.Null(repository.Inserted!.UserId);
        Assert.DoesNotContain("password", repository.Inserted.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.True(subscription.Reader.TryRead(out var activity));
        Assert.Equal("login:new", activity!.Name);
        Assert.Equal(repository.Inserted.LoginLogId, activity.Record.LoginLogId);
    }

    [Fact]
    public async Task GetLoginHistoryAsync_ReturnsRepositoryPage()
    {
        var expected = new LoginHistoryPage { Page = 2, PageSize = 10, TotalCount = 11 };
        var repository = new FakeLoginHistoryRepository { Page = expected };
        var service = new LoginHistoryService(repository, new LoginActivityEventBus());

        var result = await service.GetLoginHistoryAsync(new LoginHistoryQuery { Page = 2, PageSize = 10 });

        Assert.Same(expected, result);
    }

    private sealed class FakeLoginHistoryRepository : ILoginHistoryRepository
    {
        public LoginHistory? Inserted { get; private set; }
        public LoginHistoryPage Page { get; set; } = new();
        public Task InsertLoginHistoryAsync(LoginHistory entry) { entry.LoginLogId = 42; Inserted = entry; return Task.CompletedTask; }
        public Task<LoginHistory?> RecordLogoutAsync(string sessionId, DateTime logoutDateTime) => Task.FromResult<LoginHistory?>(null);
        public Task<int> DeleteLoginHistoryAsync(IReadOnlyCollection<long> loginLogIds) => Task.FromResult(loginLogIds.Count);
        public Task<LoginHistoryPage> GetLoginHistoryAsync(LoginHistoryQuery query) => Task.FromResult(Page);
        public Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByUserIdAsync(int userId, int page, int pageSize) => Task.FromResult<IReadOnlyList<LoginHistory>>([]);
        public Task<IReadOnlyList<LoginHistory>> GetLoginHistoryByDateRangeAsync(DateTime fromUtc, DateTime toUtc, int page, int pageSize) => Task.FromResult<IReadOnlyList<LoginHistory>>([]);
    }
}
