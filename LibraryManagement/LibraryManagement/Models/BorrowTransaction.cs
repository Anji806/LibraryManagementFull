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

    // End of the due day (UTC). Null for loans made before due dates existed:
    // those have no deadline and never accrue a fine.
    public DateTime? DueDate { get; set; }

    // Final fine, fixed when the book is returned. Active loans carry 0 here;
    // their running fine is CurrentFine below.
    public decimal FineAmount { get; set; }

    // Amount already paid towards the fine (for the future online payment).
    public decimal FinePaid { get; set; }

    // ---- Calculated by the API on every response; never stored or accepted. ----

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsOverdue { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int OverdueDays { get; set; }

    // Whole days left until the due day; null when returned or no due date.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? DaysRemaining { get; set; }

    // Fine so far: the final fine once returned, otherwise the running fine.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal CurrentFine { get; set; }

    // CurrentFine minus FinePaid.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal OutstandingFine { get; set; }

    // Borrowed, Due Soon, Overdue, Returned or Fine Pending.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DisplayStatus { get; set; } = "Borrowed";

    public Book Book { get; set; } = null!;

    public User? Member { get; set; }
}