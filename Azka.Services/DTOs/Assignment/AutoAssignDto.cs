namespace Azka.Services.DTOs.Assignment;

public class AutoAssignDto
{
    public int WorkOrderId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
}
