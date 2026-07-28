namespace Azka.Services.DTOs.Engineer;

public class UpdateEngineerDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = "08:00-16:00";
    public double DailyCapacityHours { get; set; } = 8.0;
    public bool IsActive { get; set; } = true;
}
