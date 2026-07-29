namespace Azka.Services.DTOs.Engineer;

public class EngineerAvailabilityDto
{
    public int EngineerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public double DailyCapacityHours { get; set; }
    public double CurrentLoadHours { get; set; }
    public double RemainingCapacityHours { get; set; }
}
