using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Assignments;

/// <summary>
/// Returns all assignments for a given engineer, ordered by scheduled start.
/// </summary>
public class AssignmentsByEngineerSpecification : BaseSpecification<Assignment>
{
    public AssignmentsByEngineerSpecification(int engineerId)
    {
        AddInclude(a => a.WorkOrder);
        AddInclude(a => a.Engineer);
        AddCriteria(a => a.EngineerId == engineerId);
        ApplyOrderBy(a => a.ScheduledStart);
    }
}
