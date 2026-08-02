namespace Azka.Services.DTOs.Assignment;

public class EngineerAvailabilityDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public double DailyCapacityHours { get; set; }
}
