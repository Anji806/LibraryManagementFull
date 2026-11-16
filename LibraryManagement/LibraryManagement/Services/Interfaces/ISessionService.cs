using LibraryManagement.DTOs.Auth;
using LibraryManagement.Models;

namespace LibraryManagement.Services.Interfaces;

public sealed record DeviceInfo(
    string? DeviceId,
    string DeviceName,
    string? Browser,
    string? OperatingSystem,
    string? IpAddress);

/// <param name="Session">The new session, or null when the device limit was reached.</param>
/// <param name="Limit">The device-limit challenge, or null when a session was created.</param>
/// <param name="ReplacedSessionIds">Sessions of the same browser that this sign-in replaced.</param>
public sealed record StartSessionResult(
    UserSession? Session,
    DeviceLimitDto? Limit,
    IReadOnlyList<string> ReplacedSessionIds);

public sealed record CompleteLoginResult(
    int UserId,
    UserSession Session,
    string RevokedSessionId);

public interface ISessionService
{
    /// <summary>Maximum number of simultaneously active sessions per user.</summary>
    int MaxActiveSessions { get; }

    /// <summary>
    /// Creates a session when the user has fewer than <see cref="MaxActiveSessions"/>
    /// active ones; otherwise creates a one-time challenge and returns the device list.
    /// Serialized per user, so concurrent sign-ins cannot exceed the limit.
    /// </summary>
    Task<StartSessionResult> StartAsync(int userId, DeviceInfo device);

    /// <summary>
    /// Redeems a challenge: revokes the chosen session and creates the new one atomically.
    /// Throws <see cref="ArgumentException"/> for an invalid, expired or used challenge.
    /// </summary>
    Task<CompleteLoginResult> CompleteAsync(string challengeToken, int sessionToRevokeId);

    Task<List<SessionInfoDto>> GetActiveAsync(int userId, string? currentSessionId);

    /// <summary>True when the session is active; also refreshes LastActivityAt.</summary>
    Task<bool> ValidateAsync(int userId, string sessionId);

    Task RevokeAsync(int userId, string sessionId);

    /// <summary>Revokes every active session of the user; returns their session ids.</summary>
    Task<List<string>> RevokeAllAsync(int userId);
}
