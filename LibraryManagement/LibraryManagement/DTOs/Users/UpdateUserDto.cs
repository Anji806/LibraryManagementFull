using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.DTOs.Users;

public class UpdateUserDto
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|Librarian|Member)$",
        ErrorMessage = "Role must be Admin, Librarian or Member.")]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string Address { get; set; } = string.Empty;
}
