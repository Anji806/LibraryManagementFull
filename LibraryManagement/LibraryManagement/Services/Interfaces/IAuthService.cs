using LibraryManagement.DTOs.Auth;

namespace LibraryManagement.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);

    Task<AuthResponseDto> LoginAsync(LoginDto dto);

    Task<AuthResponseDto> CompleteLoginAsync(CompleteLoginDto dto);

}
