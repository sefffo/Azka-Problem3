namespace Azka.Services.DTOs.Assignment;

public class AssignmentDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public int EngineerId { get; set; }
    public string EngineerName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
