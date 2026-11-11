using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Data.Configurations;

public class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("LoginHistory", table =>
        {
            table.HasCheckConstraint(
                "CK_LoginHistory_LoginStatus",
                "\"LoginStatus\" IN ('Success', 'Failed')");

            table.HasCheckConstraint(
                "CK_LoginHistory_FailureReason",
                "\"LoginStatus\" = 'Failed' OR \"FailureReason\" IS NULL");
        });

        builder.HasKey(x => x.LoginLogId);

        builder.Property(x => x.Username).IsRequired();
        builder.Property(x => x.LoginStatus).IsRequired();

        builder.Property(x => x.LoginDateTime)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(x => x.SessionStatus)
            .IsRequired()
            .HasDefaultValue("Unknown");

        // The history survives when the user is removed.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.UserId, x.LoginDateTime })
            .IsDescending(false, true)
            .HasDatabaseName("IX_LoginHistory_UserId_LoginDateTime");

        builder.HasIndex(x => new { x.Username, x.LoginDateTime })
            .IsDescending(false, true)
            .HasDatabaseName("IX_LoginHistory_Username_LoginDateTime");

        builder.HasIndex(x => x.LoginDateTime)
            .IsDescending()
            .HasDatabaseName("IX_LoginHistory_LoginDateTime");

        builder.HasIndex(x => x.SessionId)
            .IsUnique()
            .HasFilter("\"SessionId\" IS NOT NULL")
            .HasDatabaseName("IX_LoginHistory_SessionId");
    }
}
