namespace Azka.Services.DTOs.Assignment;

public class CreateAssignmentDto
{
    public int WorkOrderId { get; set; }
    public int EngineerId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
}
