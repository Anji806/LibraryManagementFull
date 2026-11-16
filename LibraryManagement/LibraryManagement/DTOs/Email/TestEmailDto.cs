using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.DTOs.Email;

public class TestEmailDto
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string ToEmail { get; set; } = string.Empty;
}
