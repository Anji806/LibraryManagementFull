using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Data.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");
        builder.HasKey(x => x.BookId);

        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.ISBN).IsRequired();

        builder.HasIndex(x => x.ISBN).IsUnique();

        builder.HasOne(x => x.Author)
            .WithMany(x => x.Books)
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // The book's primary category.
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // All categories of the book (BookCategories join table).
        builder.HasMany(x => x.Categories)
            .WithMany(x => x.Books)
            .UsingEntity<Dictionary<string, object>>(
                "BookCategories",
                right => right.HasOne<Category>()
                    .WithMany()
                    .HasForeignKey("CategoryId")
                    .OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Book>()
                    .WithMany()
                    .HasForeignKey("BookId")
                    .OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("BookId", "CategoryId"));
    }
}
