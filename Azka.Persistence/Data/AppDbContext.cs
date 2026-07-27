using Azka.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Azka.Persistence.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Engineer> Engineers => Set<Engineer>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentHistory> AssignmentHistories => Set<AssignmentHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
