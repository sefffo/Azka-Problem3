using Azka.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azka.Persistence.Configurations;

public class EngineerConfiguration : IEntityTypeConfiguration<Engineer>
{
    public void Configure(EntityTypeBuilder<Engineer> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EmployeeNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(e => e.EmployeeNumber).IsUnique();
        builder.Property(e => e.FullName).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Team).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Region).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Skills).HasMaxLength(500);
        builder.Property(e => e.WorkingHours).HasMaxLength(50);

        builder.HasMany(e => e.Assignments)
               .WithOne(a => a.Engineer)
               .HasForeignKey(a => a.EngineerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
