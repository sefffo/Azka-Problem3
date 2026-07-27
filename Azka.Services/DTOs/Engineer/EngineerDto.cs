namespace Azka.Services.DTOs.Engineer;

public class EngineerDto
{
    public int Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public double DailyCapacityHours { get; set; }
    public bool IsActive { get; set; }
}
