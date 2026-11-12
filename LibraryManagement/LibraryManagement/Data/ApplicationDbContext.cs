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

    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<PendingLogin> PendingLogins => Set<PendingLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            // Email and ISBN were unique ignoring case (SQLite COLLATE NOCASE).
            // citext keeps that behaviour, including for lookups and the
            // unique indexes, on PostgreSQL (supported by Neon).
            modelBuilder.HasPostgresExtension("citext");

            modelBuilder.Entity<User>()
                .Property(x => x.Email).HasColumnType("citext");

            modelBuilder.Entity<Book>()
                .Property(x => x.ISBN).HasColumnType("citext");
        }
        else
        {
            // Non-PostgreSQL providers (the SQLite-backed test host).
            modelBuilder.Entity<User>()
                .Property(x => x.Email).UseCollation("NOCASE");

            modelBuilder.Entity<Book>()
                .Property(x => x.ISBN).UseCollation("NOCASE");
        }
    }
}
