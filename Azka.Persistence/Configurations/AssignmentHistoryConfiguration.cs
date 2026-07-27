using Azka.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azka.Persistence.Configurations;

public class AssignmentHistoryConfiguration : IEntityTypeConfiguration<AssignmentHistory>
{
    public void Configure(EntityTypeBuilder<AssignmentHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.ChangedBy).IsRequired().HasMaxLength(256);
        builder.Property(h => h.ChangeReason).HasMaxLength(500);
        builder.Property(h => h.PreviousStatus).IsRequired().HasMaxLength(50);
    }
}
