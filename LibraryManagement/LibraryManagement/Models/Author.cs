namespace LibraryManagement.Models;

public class Author
{
    public int AuthorId { get; set; }

    public string AuthorName { get; set;} = string.Empty;

    public ICollection<Book> Books { get; set;}
        = new List<Book>();
}