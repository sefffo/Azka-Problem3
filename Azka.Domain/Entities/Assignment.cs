using Azka.Domain.Enums;

namespace Azka.Domain.Entities;

public class Assignment : BaseEntity<int>
{
    public int WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public int EngineerId { get; set; }
    public Engineer Engineer { get; set; } = null!;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Planned;
    public string? AssignedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<AssignmentHistory> History { get; set; } = new List<AssignmentHistory>();
}
