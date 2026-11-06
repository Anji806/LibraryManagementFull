namespace LibraryManagement.DTOs.Books;

public class BookResponseDto
{
    public int BookId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ISBN { get; set; } = string.Empty;

    public int AuthorId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public int PublishedYear { get; set; }

    public string? PreviewIntroduction { get; set; }

    public string? PreviewTheme { get; set; }

    public string? PreviewAbstract { get; set; }

    public string? PreviewMotto { get; set; }

    public string? PreviewWhyRead { get; set; }

    public string? CoverUrl { get; set; }
}
