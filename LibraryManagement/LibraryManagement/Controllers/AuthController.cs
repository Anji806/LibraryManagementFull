using LibraryManagement.DTOs.Auth;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string? CurrentSessionId =>
        User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

    // POST: api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);

        return Ok(result);
    }
    // POST: api/auth/refresh
    // Refresh token endpoint
    //this is something about authentication


    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        // Two devices are already signed in: no token, only the device list
        // and a one-time challenge for POST api/auth/complete-login.
        if (result.DeviceLimit is not null)
        {
            return StatusCode(StatusCodes.Status409Conflict, result.DeviceLimit);
        }

        return Ok(result);
    }

    // POST: api/auth/complete-login
    // Logs out the chosen device and finishes the pending sign-in.
    [HttpPost("complete-login")]
    public async Task<IActionResult> CompleteLogin(CompleteLoginDto dto)
    {
        return Ok(await _authService.CompleteLoginAsync(dto));
    }

    // GET: api/auth/sessions  (the signed-in user's own active devices)
    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromServices] ISessionService sessions)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        return Ok(await sessions.GetActiveAsync(userId, CurrentSessionId));
    }

    // POST: api/auth/logout-all
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(
        [FromServices] ISessionService sessions,
        [FromServices] ILoginHistoryService history)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        foreach (var sessionId in await sessions.RevokeAllAsync(userId))
        {
            await history.RecordLogoutAsync(sessionId, DateTime.UtcNow);
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromServices] ILoginHistoryService history,
        [FromServices] ISessionService sessions)
    {
        var sessionId = CurrentSessionId;
        if (string.IsNullOrWhiteSpace(sessionId) || CurrentUserId is not { } userId) return Unauthorized();

        // Releases this device's slot immediately.
        await sessions.RevokeAsync(userId, sessionId);
        await history.RecordLogoutAsync(sessionId, DateTime.UtcNow);
        return NoContent();
    }

}
