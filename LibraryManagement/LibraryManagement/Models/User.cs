namespace LibraryManagement.Models;

public class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    // Never serialized: borrow responses include the Member user.
    [System.Text.Json.Serialization.JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Member";

    public bool IsActive { get; set; } = true;
    public string Address { get; set; } = string.Empty;

    public ICollection<BorrowTransaction> BorrowTransactions { get; set; }
        = new List<BorrowTransaction>();
}
