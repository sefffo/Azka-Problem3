using Azka.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azka.Persistence.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.WorkOrderNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(w => w.WorkOrderNumber).IsUnique();
        builder.Property(w => w.MaintenanceType).HasConversion<string>();
        builder.Property(w => w.Priority).HasConversion<string>();
        builder.Property(w => w.Status).HasConversion<string>();
        builder.Property(w => w.Notes).HasMaxLength(1000);

        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.Priority);
        builder.HasIndex(w => w.DueDate);

        builder.HasMany(w => w.Assignments)
               .WithOne(a => a.WorkOrder)
               .HasForeignKey(a => a.WorkOrderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
