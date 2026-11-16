using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.DTOs.Auth;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    /// <summary>The token from the emailed link.</summary>
    [Required]
    [StringLength(200)]
    public string Token { get; set; } = string.Empty;

    // Same rule as registration (RegisterDto).
    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
