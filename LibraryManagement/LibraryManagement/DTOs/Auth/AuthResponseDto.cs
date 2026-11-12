namespace LibraryManagement.DTOs.Auth;
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    // Set instead of a token when the device limit was reached (sent as HTTP 409).
    [System.Text.Json.Serialization.JsonIgnore]
    public DeviceLimitDto? DeviceLimit { get; set; }
}