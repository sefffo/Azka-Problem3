using Azka.Domain.Enums;

namespace Azka.Domain.Entities;

public class Engineer : BaseEntity<int>
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = "08:00-16:00";
    public double DailyCapacityHours { get; set; } = 8.0;
    public bool IsActive { get; set; } = true;

    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
