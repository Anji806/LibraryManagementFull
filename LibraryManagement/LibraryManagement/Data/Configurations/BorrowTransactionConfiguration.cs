using LibraryManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Data.Configurations;

public class BorrowTransactionConfiguration
    : IEntityTypeConfiguration<BorrowTransaction>
{
    public void Configure(EntityTypeBuilder<BorrowTransaction> builder)
    {
        builder.ToTable("BorrowTransactions");
        builder.HasKey(x => x.TransactionId);

        builder.Property(x => x.Status).IsRequired();

        builder.HasOne(x => x.Book)
            .WithMany(x => x.BorrowTransactions)
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        // The history row survives when the member's account is removed.
        builder.HasOne(x => x.Member)
            .WithMany(x => x.BorrowTransactions)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
