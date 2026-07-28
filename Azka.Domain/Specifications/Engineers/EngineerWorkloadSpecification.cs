using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Returns assignments for an engineer that overlap a given day window.
/// Used by EngineerService.GetWorkloadAsync() to calculate daily utilization.
/// </summary>
public class EngineerWorkloadSpecification : BaseSpecification<Assignment>
{
    public EngineerWorkloadSpecification(int engineerId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        AddInclude(a => a.WorkOrder);
        AddCriteria(a =>
            a.EngineerId == engineerId &&
            a.ScheduledStart < dayEnd &&
            a.ScheduledEnd > dayStart &&
            a.Status != AssignmentStatus.Cancelled &&
            a.Status != AssignmentStatus.Failed);
    }
}
