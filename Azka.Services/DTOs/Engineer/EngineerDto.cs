namespace Azka.Services.DTOs.Engineer;

public class EngineerDto
{
    public int    Id                 { get; set; }
    public string EmployeeNumber     { get; set; } = string.Empty;
    public string FullName           { get; set; } = string.Empty;
    public string Email              { get; set; } = string.Empty;
    public string Team               { get; set; } = string.Empty;
    public string Region             { get; set; } = string.Empty;
    public string Skills             { get; set; } = string.Empty;
    public string WorkingHours       { get; set; } = string.Empty;
    public double DailyCapacityHours { get; set; }
    public bool   IsActive           { get; set; }

    /// <summary>Total estimated hours of active assignments overlapping today.</summary>
    public double BookedHoursToday { get; set; }

    /// <summary>BookedHoursToday / DailyCapacityHours as a percentage (0-100+, can exceed 100).</summary>
    public double UtilizationPercentage { get; set; }
}
