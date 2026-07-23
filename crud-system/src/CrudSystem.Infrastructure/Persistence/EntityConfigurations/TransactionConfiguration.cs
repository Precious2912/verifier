using CrudSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrudSystem.Infrastructure.Persistence.EntityConfigurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.Reference);
        builder.Property(t => t.Reference).IsRequired().HasMaxLength(50);
        builder.Property(t => t.DebitAccount).IsRequired().HasMaxLength(30);
        builder.Property(t => t.CreditAccount).IsRequired().HasMaxLength(30);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Type).HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ModifiedAt);
    }
}