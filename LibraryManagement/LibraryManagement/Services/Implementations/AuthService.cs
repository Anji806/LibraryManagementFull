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

    public AuthService(
        IAuthRepository authRepository,
        JwtTokenGenerator jwtTokenGenerator,
        ILoginHistoryService loginHistory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthService> logger)
    {
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

        var token =
            _jwtTokenGenerator.GenerateToken(user);

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

        var sessionId = Guid.NewGuid().ToString("N");
        var expiryMinutes = int.TryParse(_httpContextAccessor.HttpContext?.RequestServices.GetService<IConfiguration>()?["Jwt:ExpiryMinutes"], out var configuredExpiry) ? configuredExpiry : 60;
        var sessionExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var token = _jwtTokenGenerator.GenerateToken(user, sessionId);

        await RecordLoginAttemptAsync(dto.Email, user.UserId, "Success", null, sessionId, sessionExpiresAt);

        return new AuthResponseDto
        {
            Token = token,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        };
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
