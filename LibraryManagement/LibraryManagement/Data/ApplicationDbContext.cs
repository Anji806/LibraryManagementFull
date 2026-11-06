using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Book> Books => Set<Book>();

    public DbSet<BorrowTransaction> BorrowTransactions
        => Set<BorrowTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Book>()
            .HasIndex(x => x.ISBN)
            .IsUnique();

        modelBuilder.Entity<Book>()
            .HasOne(x => x.Author)
            .WithMany(x => x.Books)
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Book>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Book>()
            .HasMany(x => x.Categories)
            .WithMany(x => x.Books)
            .UsingEntity<Dictionary<string, object>>(
                "BookCategories",
                right => right.HasOne<Category>().WithMany().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Restrict),
                left => left.HasOne<Book>().WithMany().HasForeignKey("BookId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("BookId", "CategoryId"));

        modelBuilder.Entity<BorrowTransaction>()
            .HasOne(x => x.Book)
            .WithMany(x => x.BorrowTransactions)
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BorrowTransaction>()
            .HasOne(x => x.Member)
            .WithMany(x => x.BorrowTransactions)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
