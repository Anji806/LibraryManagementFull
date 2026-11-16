using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NotificationType).IsRequired();
        builder.Property(x => x.DedupeKey).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BorrowTransaction>()
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // The duplicate guard: one row per distinct notification, ever.
        builder.HasIndex(x => x.DedupeKey).IsUnique();

        builder.HasIndex(x => new { x.Status, x.LastAttemptAt });
        builder.HasIndex(x => x.UserId);
    }
}

public class BookAvailabilityRequestConfiguration : IEntityTypeConfiguration<BookAvailabilityRequest>
{
    public void Configure(EntityTypeBuilder<BookAvailabilityRequest> builder)
    {
        builder.ToTable("BookAvailabilityRequests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // A member can wait for a given book only once at a time.
        builder.HasIndex(x => new { x.UserId, x.BookId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Waiting'")
            .HasDatabaseName("IX_BookAvailabilityRequests_UserId_BookId_Waiting");

        builder.HasIndex(x => new { x.BookId, x.Status, x.CreatedAt });
    }
}
