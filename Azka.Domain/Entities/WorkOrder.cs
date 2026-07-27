using Azka.Domain.Enums;

namespace Azka.Domain.Entities;

public class WorkOrder : BaseEntity<int>
{
    public string WorkOrderNumber { get; set; } = string.Empty;
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public MaintenanceType MaintenanceType { get; set; }
    public Priority Priority { get; set; }
    public double EstimatedHours { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime DueDate { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
