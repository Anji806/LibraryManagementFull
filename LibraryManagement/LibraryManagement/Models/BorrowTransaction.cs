using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Models;

public class BorrowTransaction
{
    [Key]
    public int TransactionId { get; set; }

    public int BookId { get; set; }

    // Null once the member's account has been removed; the history row is
    // kept and MemberName / MemberEmail hold a snapshot of who borrowed.
    public int? MemberId { get; set; }

    public string? MemberName { get; set; }

    public string? MemberEmail { get; set; }

    public DateTime BorrowDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public string Status { get; set; } = "Borrowed";

    public Book Book { get; set; } = null!;

    public User? Member { get; set; }
}