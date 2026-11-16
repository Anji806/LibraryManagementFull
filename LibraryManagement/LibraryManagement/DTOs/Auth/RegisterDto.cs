namespace LibraryManagement.DTOs.Auth;
using System.ComponentModel.DataAnnotations;

public class RegisterDto
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    // Persistent browser-profile id from the client (identifies a browser, not a person).
    [MaxLength(64)]
    public string? DeviceId { get; set; }

    [MaxLength(100)]
    public string? DeviceName { get; set; }
}