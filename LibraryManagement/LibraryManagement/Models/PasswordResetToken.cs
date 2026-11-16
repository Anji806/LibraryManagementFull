namespace LibraryManagement.Models;

/// <summary>
/// One password-reset request. Only a SHA-256 hash of the emailed token is
/// stored, so a leaked database cannot be used to reset anyone's password.
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }
}
