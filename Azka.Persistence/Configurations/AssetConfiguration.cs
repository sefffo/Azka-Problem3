using Azka.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azka.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssetNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(a => a.AssetNumber).IsUnique();
        builder.Property(a => a.Address).IsRequired().HasMaxLength(300);
        builder.Property(a => a.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(a => a.AssetType).HasConversion<string>();
        builder.Property(a => a.Status).HasConversion<string>();

        builder.HasMany(a => a.WorkOrders)
               .WithOne(w => w.Asset)
               .HasForeignKey(w => w.AssetId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
