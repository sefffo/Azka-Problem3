using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Assignments;

/// <summary>
/// Returns non-cancelled/non-failed assignments that overlap a given day
/// for any of the provided engineers. Used by EngineerService.GetAllAsync()
/// to show each engineer's booked hours for today.
/// </summary>
public class AssignmentsByEngineersOnDateSpecification : BaseSpecification<Assignment>
{
    public AssignmentsByEngineersOnDateSpecification(IEnumerable<int> engineerIds, DateTime date)
    {
        var ids      = engineerIds.ToList();
        var dayStart = date.Date;
        var dayEnd   = dayStart.AddDays(1);

        AddInclude(a => a.WorkOrder);
        AddCriteria(a =>
            ids.Contains(a.EngineerId) &&
            a.ScheduledStart < dayEnd &&
            a.ScheduledEnd > dayStart &&
            a.Status != AssignmentStatus.Cancelled &&
            a.Status != AssignmentStatus.Failed);
    }
}
