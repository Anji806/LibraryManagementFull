namespace LibraryManagement.Models;

public class Book
{
    public int BookId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ISBN { get; set; } = string.Empty;

    public int AuthorId { get; set; }

    public int CategoryId { get; set; }

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public int PublishedYear { get; set; }

    public string? PreviewIntroduction { get; set; }

    public string? PreviewTheme { get; set; }

    public string? PreviewAbstract { get; set; }

    public string? PreviewMotto { get; set; }

    public string? PreviewWhyRead { get; set; }

    public string? CoverUrl { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CategoryNames { get; set; }

    public Author Author { get; set; } = null!;

    public Category Category { get; set; } = null!;

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public ICollection<BorrowTransaction> BorrowTransactions { get; set; }
        = new List<BorrowTransaction>();
}
