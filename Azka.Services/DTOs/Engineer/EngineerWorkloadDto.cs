namespace Azka.Services.DTOs.Engineer;

public class EngineerWorkloadDto
{
    public int EngineerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int AssignedWorkOrders { get; set; }
    public double TotalEstimatedHours { get; set; }
    public double DailyCapacityHours { get; set; }
    public double RemainingCapacityHours { get; set; }
    public double UtilizationPercentage { get; set; }
}
