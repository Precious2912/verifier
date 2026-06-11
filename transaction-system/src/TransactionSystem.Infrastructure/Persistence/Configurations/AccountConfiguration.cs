using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionSystem.Domain.Entities;

namespace TransactioSystem.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(30);
        builder.Property(a => a.AccountName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Balance).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ModifiedAt);
    }
}