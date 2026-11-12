using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LibraryManagement.Data;
using LibraryManagement.DTOs.Auth;
using LibraryManagement.Models;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Services.Implementations;

public class SessionService : ISessionService
{
    private const int LockNamespace = 7301;
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ActivityWriteInterval = TimeSpan.FromMinutes(1);

    // SQLite (local development only) has no advisory locks; it serializes
    // writers itself, so an in-process gate per user is enough there.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> LocalGates = new();

    private readonly ApplicationDbContext _db;
    private readonly int _sessionMinutes;

    public SessionService(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _sessionMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes)
            ? minutes
            : 60;
    }

    public int MaxActiveSessions => 2;

    public Task<StartSessionResult> StartAsync(int userId, DeviceInfo device)
        => WithUserLockAsync(userId, async () =>
        {
            var now = DateTime.UtcNow;
            var active = await ActiveSessions(userId, now)
                .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
                .ToListAsync();

            // Signing in again from the same browser replaces its old session
            // instead of using up a second slot.
            var replaced = new List<string>();
            if (!string.IsNullOrEmpty(device.DeviceId))
            {
                foreach (var same in active.Where(s => s.DeviceId == device.DeviceId).ToList())
                {
                    Revoke(same, now);
                    replaced.Add(same.SessionId);
                    active.Remove(same);
                }
            }

            if (active.Count < MaxActiveSessions)
            {
                var session = NewSession(userId, device, now);
                _db.UserSessions.Add(session);
                await _db.SaveChangesAsync();
                return new StartSessionResult(session, null, replaced);
            }

            // Housekeeping: stale challenges are useless.
            await _db.PendingLogins
                .Where(p => p.ExpiresAt < now.AddDays(-1))
                .ExecuteDeleteAsync();

            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _db.PendingLogins.Add(new PendingLogin
            {
                UserId = userId,
                TokenHash = Hash(token),
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                Browser = device.Browser,
                OperatingSystem = device.OperatingSystem,
                IpAddress = device.IpAddress,
                CreatedAt = now,
                ExpiresAt = now + ChallengeLifetime
            });
            await _db.SaveChangesAsync();

            return new StartSessionResult(
                null,
                new DeviceLimitDto
                {
                    ChallengeToken = token,
                    ExpiresInSeconds = (int)ChallengeLifetime.TotalSeconds,
                    Message = "You are already logged in on two devices. " +
                              "Select one device to log out and continue.",
                    Sessions = active.Select(s => ToDto(s, null)).ToList()
                },
                replaced);
        });

    public async Task<CompleteLoginResult> CompleteAsync(
        string challengeToken,
        int sessionToRevokeId)
    {
        var hash = Hash(challengeToken ?? string.Empty);

        // Resolve the user first so the rest can run under that user's lock.
        var userId = await _db.PendingLogins
            .AsNoTracking()
            .Where(p => p.TokenHash == hash)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync();

        if (userId is null)
        {
            throw new ArgumentException(
                "This sign-in request is no longer valid. Please sign in again.");
        }

        return await WithUserLockAsync(userId.Value, async () =>
        {
            var now = DateTime.UtcNow;

            // Re-read under the lock: single use, short lived.
            var pending = await _db.PendingLogins.FirstOrDefaultAsync(p =>
                p.TokenHash == hash && p.ConsumedAt == null && p.ExpiresAt > now);

            if (pending is null)
            {
                throw new ArgumentException(
                    "This sign-in request has expired. Please sign in again.");
            }

            var target = await ActiveSessions(pending.UserId, now)
                .FirstOrDefaultAsync(s => s.Id == sessionToRevokeId);

            if (target is null)
            {
                throw new ArgumentException(
                    "That device is no longer signed in. Please sign in again.");
            }

            Revoke(target, now);
            pending.ConsumedAt = now;

            var remaining = await ActiveSessions(pending.UserId, now)
                .CountAsync(s => s.Id != target.Id);

            if (remaining >= MaxActiveSessions)
            {
                // Someone else signed in meanwhile; nothing is saved.
                throw new ArgumentException(
                    "The list of signed-in devices changed. Please sign in again.");
            }

            var session = NewSession(
                pending.UserId,
                new DeviceInfo(
                    pending.DeviceId,
                    pending.DeviceName,
                    pending.Browser,
                    pending.OperatingSystem,
                    pending.IpAddress),
                now);

            _db.UserSessions.Add(session);
            await _db.SaveChangesAsync();

            return new CompleteLoginResult(pending.UserId, session, target.SessionId);
        });
    }

    public async Task<List<SessionInfoDto>> GetActiveAsync(int userId, string? currentSessionId)
    {
        var sessions = await ActiveSessions(userId, DateTime.UtcNow)
            .AsNoTracking()
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .ToListAsync();

        return sessions.Select(s => ToDto(s, currentSessionId)).ToList();
    }

    public async Task<bool> ValidateAsync(int userId, string sessionId)
    {
        var now = DateTime.UtcNow;

        var session = await ActiveSessions(userId, now)
            .AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .Select(s => new { s.Id, s.LastActivityAt })
            .FirstOrDefaultAsync();

        if (session is null)
        {
            return false;
        }

        if (now - session.LastActivityAt >= ActivityWriteInterval)
        {
            await _db.UserSessions
                .Where(s => s.Id == session.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastActivityAt, now));
        }

        return true;
    }

    public async Task RevokeAsync(int userId, string sessionId)
    {
        var now = DateTime.UtcNow;

        await _db.UserSessions
            .Where(s => s.UserId == userId && s.SessionId == sessionId && !s.IsRevoked)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.IsRevoked, true)
                .SetProperty(s => s.RevokedAt, now));
    }

    public async Task<List<string>> RevokeAllAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var ids = await ActiveSessions(userId, now)
            .Select(s => s.SessionId)
            .ToListAsync();

        await _db.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.IsRevoked, true)
                .SetProperty(s => s.RevokedAt, now));

        return ids;
    }

    // ------------------------------------------------------------------

    private IQueryable<UserSession> ActiveSessions(int userId, DateTime now)
        => _db.UserSessions.Where(s =>
            s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now);

    private UserSession NewSession(int userId, DeviceInfo device, DateTime now)
        => new()
        {
            UserId = userId,
            SessionId = Guid.NewGuid().ToString("N"),
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            Browser = device.Browser,
            OperatingSystem = device.OperatingSystem,
            IpAddress = device.IpAddress,
            CreatedAt = now,
            LastActivityAt = now,
            ExpiresAt = now.AddMinutes(_sessionMinutes)
        };

    private static void Revoke(UserSession session, DateTime now)
    {
        session.IsRevoked = true;
        session.RevokedAt = now;
    }

    private static SessionInfoDto ToDto(UserSession s, string? currentSessionId)
        => new()
        {
            Id = s.Id,
            DeviceName = s.DeviceName,
            Browser = s.Browser,
            OperatingSystem = s.OperatingSystem,
            IpAddress = s.IpAddress,
            CreatedAt = s.CreatedAt,
            LastActivityAt = s.LastActivityAt,
            IsCurrent = currentSessionId != null && s.SessionId == currentSessionId
        };

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction holding a per-user lock
    /// (PostgreSQL advisory transaction lock), so two sign-ins for the same
    /// user are counted one after the other.
    /// </summary>
    private async Task<T> WithUserLockAsync<T>(int userId, Func<Task<T>> work)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            SemaphoreSlim? gate = null;

            if (!_db.Database.IsNpgsql())
            {
                gate = LocalGates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync();
            }

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                if (_db.Database.IsNpgsql())
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock({LockNamespace}, {userId})");
                }

                var result = await work();
                await transaction.CommitAsync();
                return result;
            }
            finally
            {
                gate?.Release();
            }
        });
    }
}
