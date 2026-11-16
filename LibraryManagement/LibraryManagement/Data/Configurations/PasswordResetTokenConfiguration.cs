using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash).IsUnique();

        // At most one usable token per user: a new request must retire the
        // previous one first, and two racing requests cannot both succeed.
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("\"IsUsed\" = false")
            .HasDatabaseName("IX_PasswordResetTokens_UserId_Active");
    }
}
