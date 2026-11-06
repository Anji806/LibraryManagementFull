using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using LibraryManagement.DTOs.Auth;
using LibraryManagement.Data;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Implementations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Tests;

public sealed class LoginHistoryDeleteApiTests
{
    private const string Password = "Test@12345";

    [Fact]
    public async Task DeleteSelectedHistory_IsAdminOnlyAndDoesNotDeleteUserOrRevokeJwt()
    {
        using var factory = new LibraryApiFactory();
        var email = $"admin-{Guid.NewGuid():N}@test.local";
        using var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Admin", email, password = Password });
        register.EnsureSuccessStatusCode();
        var registered = (await register.Content.ReadFromJsonAsync<AuthResponseDto>())!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
            user.Role = "Admin";
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using (var invalidRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/login-history")
        {
            Content = JsonContent.Create(new { loginLogIds = new[] { 0 } })
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalidRequest)).StatusCode);

        LoginHistory failed;
        LoginHistory successful;
        using (var scope = factory.Services.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IDapperUnitOfWork>();
            var repository = new LoginHistoryRepository(uow);
            failed = new LoginHistory { UserId = registered.UserId, Username = email, LoginDateTime = DateTime.UtcNow, LoginStatus = "Failed", FailureReason = "Invalid credentials." };
            successful = new LoginHistory { UserId = failed.UserId, Username = email, LoginDateTime = DateTime.UtcNow, LoginStatus = "Success", SessionId = Guid.NewGuid().ToString("N"), SessionStatus = "Active" };
            await repository.InsertLoginHistoryAsync(failed);
            await repository.InsertLoginHistoryAsync(successful);
        }

        using var unauthorized = factory.CreateClient();
        using var unauthorizedRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/login-history")
        {
            Content = JsonContent.Create(new { loginLogIds = new[] { failed.LoginLogId } })
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthorized.SendAsync(unauthorizedRequest)).StatusCode);

        using var staleSelection = new HttpRequestMessage(HttpMethod.Delete, "/api/login-history")
        {
            Content = JsonContent.Create(new { loginLogIds = new[] { failed.LoginLogId, long.MaxValue } })
        };
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(staleSelection)).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IDapperUnitOfWork>();
            var connection = await uow.GetConnectionAsync();
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LoginHistory WHERE LoginLogId = @id", new { id = failed.LoginLogId }));
        }

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/login-history")
        {
            Content = JsonContent.Create(new { loginLogIds = new[] { failed.LoginLogId, successful.LoginLogId } })
        };
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<DeleteResult>();
        Assert.Equal(2, deleted!.DeletedCount);

        LoginHistory activeSessionRecord;
        using (var scope = factory.Services.CreateScope())
        {
            var repository = new LoginHistoryRepository(scope.ServiceProvider.GetRequiredService<IDapperUnitOfWork>());
            activeSessionRecord = new LoginHistory
            {
                UserId = registered.UserId,
                Username = email,
                LoginDateTime = DateTime.UtcNow,
                LoginStatus = "Success",
                SessionId = Guid.NewGuid().ToString("N"),
                SessionStatus = "Active"
            };
            await repository.InsertLoginHistoryAsync(activeSessionRecord);
        }

        using var singleDeleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/login-history")
        {
            Content = JsonContent.Create(new { loginLogIds = new[] { activeSessionRecord.LoginLogId } })
        };
        var singleDeleteResponse = await client.SendAsync(singleDeleteRequest);
        Assert.Equal(HttpStatusCode.OK, singleDeleteResponse.StatusCode);
        Assert.Equal(1, (await singleDeleteResponse.Content.ReadFromJsonAsync<DeleteResult>())!.DeletedCount);

        using (var scope = factory.Services.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IDapperUnitOfWork>();
            var connection = await uow.GetConnectionAsync();
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LoginHistory WHERE LoginLogId IN @ids", new { ids = new[] { failed.LoginLogId, successful.LoginLogId, activeSessionRecord.LoginLogId } }));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users WHERE Email = @email", new { email }));
        }

        // The API uses stateless JWT authentication; deleting a history row does not revoke the token.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/login-history")).StatusCode);

        using var member = factory.CreateClient();
        var memberEmail = $"member-{Guid.NewGuid():N}@test.local";
        var memberRegister = await member.PostAsJsonAsync("/api/auth/register", new { fullName = "Member", email = memberEmail, password = Password });
        memberRegister.EnsureSuccessStatusCode();
        var memberLogin = await member.PostAsJsonAsync("/api/auth/login", new { email = memberEmail, password = Password });
        memberLogin.EnsureSuccessStatusCode();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await memberLogin.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token);
        using var forbiddenRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/login-history")
        {
            Content = JsonContent.Create(new { loginLogIds = new[] { failed.LoginLogId } })
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(forbiddenRequest)).StatusCode);
    }

    private sealed class DeleteResult
    {
        public int DeletedCount { get; set; }
    }
}
