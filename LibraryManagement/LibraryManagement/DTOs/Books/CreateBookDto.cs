using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.DTOs.Books;

public class CreateBookDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string ISBN { get; set; } = string.Empty;

    [Required]
    public int AuthorId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Range(1, 10000)]
    public int TotalCopies { get; set; }

    [Range(1, 9999)]
    public int PublishedYear { get; set; }

    [MaxLength(12000)]
    public string? PreviewIntroduction { get; set; }

    [MaxLength(12000)]
    public string? PreviewTheme { get; set; }

    [MaxLength(12000)]
    public string? PreviewAbstract { get; set; }

    [MaxLength(1000)]
    public string? PreviewMotto { get; set; }

    [MaxLength(3000)]
    public string? PreviewWhyRead { get; set; }

    [MaxLength(1000)]
    public string? CoverUrl { get; set; }
}
