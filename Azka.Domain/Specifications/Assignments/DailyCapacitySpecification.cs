using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Assignments;

/// <summary>
/// Returns all active assignments for an engineer on a specific day.
/// Used to sum EstimatedHours and enforce the daily capacity limit.
/// </summary>
public class DailyCapacitySpecification : BaseSpecification<Assignment>
{
    public DailyCapacitySpecification(int engineerId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        AddInclude(a => a.WorkOrder);
        AddCriteria(a =>
            a.EngineerId == engineerId &&
            a.ScheduledStart >= dayStart &&
            a.ScheduledStart < dayEnd &&
            a.Status != AssignmentStatus.Cancelled &&
            a.Status != AssignmentStatus.Failed);
    }
}
