using Azka.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azka.Persistence.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasConversion<string>();
        builder.Property(a => a.AssignedBy).HasMaxLength(256);

        builder.Property(a => a.RowVersion)
               .IsRowVersion()
               .IsConcurrencyToken(); // 3shan nt2ked mn el mshakel bta3et el double assignments fa nt2ked mn all or none commit 

        
        // intensive reads on this table
        builder.HasIndex(a => new { a.EngineerId, a.ScheduledStart, a.ScheduledEnd });
        builder.HasIndex(a => a.Status);

        builder.HasMany(a => a.History)
               .WithOne(h => h.Assignment)
               .HasForeignKey(h => h.AssignmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
