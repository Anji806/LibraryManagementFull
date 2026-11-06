namespace LibraryManagement.Models;

public class Category
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public ICollection<Book> Books { get; set;}
        = new List<Book>();
}
