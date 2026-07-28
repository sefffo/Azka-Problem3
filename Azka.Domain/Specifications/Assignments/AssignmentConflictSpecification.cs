using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Assignments;

/// <summary>
/// Detects time-slot conflicts for a given engineer.
/// Uses the standard interval overlap algorithm:
///   existing.Start < new.End  AND  existing.End > new.Start
///
/// Pass excludeAssignmentId when rescheduling so the assignment
/// being moved does not conflict with its own current slot.
/// </summary>
public class AssignmentConflictSpecification : BaseSpecification<Assignment>
{
    public AssignmentConflictSpecification(
        int engineerId,
        DateTime newStart,
        DateTime newEnd,
        int? excludeAssignmentId = null)
    {
        AddCriteria(a =>
            a.EngineerId == engineerId &&
            (excludeAssignmentId == null || a.Id != excludeAssignmentId) &&
            a.Status != AssignmentStatus.Cancelled &&
            a.Status != AssignmentStatus.Failed &&
            a.ScheduledStart < newEnd &&
            a.ScheduledEnd > newStart);
    }
}
