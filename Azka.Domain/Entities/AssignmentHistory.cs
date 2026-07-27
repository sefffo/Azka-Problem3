namespace Azka.Domain.Entities;

public class AssignmentHistory : BaseEntity<int>
{
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
    public DateTime PreviousStart { get; set; }
    public DateTime PreviousEnd { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
