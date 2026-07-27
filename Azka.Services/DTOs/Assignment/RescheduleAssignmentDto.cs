namespace Azka.Services.DTOs.Assignment;

public class RescheduleAssignmentDto
{
    public DateTime NewScheduledStart { get; set; }
    public DateTime NewScheduledEnd { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}
