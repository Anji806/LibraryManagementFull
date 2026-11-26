using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.DTOs.Auth;

public class GoogleLoginDto
{
    // The Google ID token ("credential") returned by Google Identity Services.
    [Required]
    public string IdToken { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? DeviceId { get; set; }

    [MaxLength(100)]
    public string? DeviceName { get; set; }
}
