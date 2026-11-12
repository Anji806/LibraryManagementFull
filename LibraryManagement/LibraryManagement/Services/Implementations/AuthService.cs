using LibraryManagement.DTOs.Auth;
using LibraryManagement.Exceptions;
using LibraryManagement.Helpers;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Services.Implementations;

public class AuthService : IAuthService

{
    private readonly IAuthRepository _authRepository;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly ILoginHistoryService _loginHistory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthService> _logger;
    private readonly ISessionService _sessions;

    public AuthService(
        IAuthRepository authRepository,
        JwtTokenGenerator jwtTokenGenerator,
        ILoginHistoryService loginHistory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthService> logger,
        ISessionService sessions)
    {
        _sessions = sessions;
        _authRepository = authRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = new PasswordHasher<User>();
        _loginHistory = loginHistory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterDto dto)
    {
        var existingUser =
            await _authRepository.GetByEmailAsync(dto.Email);

        if (existingUser != null)
        {
            throw new BusinessRuleException(
                "An account with this email already exists.");
        }

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Role = "Member",
            IsActive = true
        };

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                dto.Password);

        await _authRepository.AddAsync(user);
        await _authRepository.SaveChangesAsync();

        // A new account has no sessions, so this never hits the device limit.
        var started = await _sessions.StartAsync(
            user.UserId, BuildDevice(dto.DeviceId, dto.DeviceName));

        var token =
            _jwtTokenGenerator.GenerateToken(user, started.Session!.SessionId);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        };
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginDto dto)
    {
        var user =
            await _authRepository.GetByEmailAsync(dto.Email);

        if (user == null)
        {
            await RecordLoginAttemptAsync(dto.Email, null, "Failed", "Invalid credentials.");
            throw new BusinessRuleException(
                "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            await RecordLoginAttemptAsync(dto.Email, user.UserId, "Failed", "Account inactive.");
            throw new BusinessRuleException(
                "This account is inactive.");
        }

        var passwordResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password);

        if (passwordResult ==
            PasswordVerificationResult.Failed)
        {
            await RecordLoginAttemptAsync(dto.Email, user.UserId, "Failed", "Invalid credentials.");
            throw new BusinessRuleException(
                "Invalid email or password.");
        }

        // Credentials are verified; now apply the two-device limit.
        var started = await _sessions.StartAsync(
            user.UserId, BuildDevice(dto.DeviceId, dto.DeviceName));

        await RecordLogoutsAsync(started.ReplacedSessionIds);

        if (started.Limit is not null)
        {
            return new AuthResponseDto { DeviceLimit = started.Limit };
        }

        return await SignInAsync(user, started.Session!, dto.Email);
    }

    public async Task<AuthResponseDto> CompleteLoginAsync(CompleteLoginDto dto)
    {
        var result = await _sessions.CompleteAsync(
            dto.ChallengeToken, dto.SessionToRevokeId!.Value);

        await RecordLogoutsAsync([result.RevokedSessionId]);

        var user = await _authRepository.GetByIdAsync(result.UserId);

        if (user == null || !user.IsActive)
        {
            await _sessions.RevokeAsync(result.UserId, result.Session.SessionId);
            throw new BusinessRuleException("This account is inactive.");
        }

        return await SignInAsync(user, result.Session, user.Email);
    }

    private async Task<AuthResponseDto> SignInAsync(User user, UserSession session, string username)
    {
        var token = _jwtTokenGenerator.GenerateToken(user, session.SessionId);

        await RecordLoginAttemptAsync(
            username, user.UserId, "Success", null, session.SessionId, session.ExpiresAt);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        };
    }

    private async Task RecordLogoutsAsync(IEnumerable<string> sessionIds)
    {
        foreach (var sessionId in sessionIds)
        {
            try { await _loginHistory.RecordLogoutAsync(sessionId, DateTime.UtcNow); }
            catch (Exception exception)
            {
                // The audit trail must not change the authentication result.
                _logger.LogError(exception, "Unable to write logout audit record.");
            }
        }
    }

    private DeviceInfo BuildDevice(string? deviceId, string? deviceName)
    {
        var context = _httpContextAccessor.HttpContext;
        var agent = context?.Request.Headers.UserAgent.ToString();
        var browser = DetectBrowser(agent);
        var os = DetectOperatingSystem(agent);

        return new DeviceInfo(
            Limit(deviceId?.Trim(), 64),
            Limit(deviceName?.Trim(), 100)
                ?? $"{browser ?? "Unknown browser"} on {os ?? "unknown system"}",
            browser,
            os,
            Limit(GetClientIp(context), 45));
    }

    private async Task RecordLoginAttemptAsync(string username, int? userId, string status, string? reason, string? sessionId = null, DateTime? sessionExpiresAt = null)
    {
        var context = _httpContextAccessor.HttpContext;
        var userAgent = context?.Request.Headers.UserAgent.ToString();
        try
        {
            await _loginHistory.RecordLoginAsync(new LoginHistory
            {
                UserId = userId,
                Username = Limit(username, 320) ?? string.Empty,
                LoginDateTime = DateTime.UtcNow,
                IPAddress = Limit(GetClientIp(context), 45),
                Browser = Limit(DetectBrowser(userAgent), 256),
                OperatingSystem = Limit(DetectOperatingSystem(userAgent), 256),
                LoginStatus = status,
                FailureReason = reason,
                SessionId = sessionId,
                SessionExpiresAt = sessionExpiresAt,
                SessionStatus = status == "Success" ? "Active" : "Failed"
            });
        }
        catch (Exception exception)
        {
            // Authentication result is authoritative; audit storage must not alter it.
            _logger.LogError(exception, "Unable to write login audit record.");
        }
    }

    private static string? GetClientIp(HttpContext? context)
    {
        var address = context?.Connection.RemoteIpAddress;
        if (address is null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.ToString();
    }

    private static string? Limit(string? value, int length)
        => string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, length)];

    private static string? DetectBrowser(string? agent)
    {
        if (string.IsNullOrWhiteSpace(agent)) return null;
        if (agent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (agent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (agent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (agent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
        return "Other";
    }

    private static string? DetectOperatingSystem(string? agent)
    {
        if (string.IsNullOrWhiteSpace(agent)) return null;
        if (agent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (agent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
        if (agent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || agent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iOS";
        if (agent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) || agent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "macOS";
        if (agent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
        return "Other";
    }

}
