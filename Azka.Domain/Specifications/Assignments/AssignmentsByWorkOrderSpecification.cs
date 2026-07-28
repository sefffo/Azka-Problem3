using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Assignments;

/// <summary>
/// Returns all assignments for a specific work order, newest first.
/// Used by AssignmentService.GetByWorkOrderAsync().
/// </summary>
public class AssignmentsByWorkOrderSpecification : BaseSpecification<Assignment>
{
    public AssignmentsByWorkOrderSpecification(int workOrderId)
    {
        AddInclude(a => a.Engineer);
        AddInclude(a => a.WorkOrder);
        AddCriteria(a => a.WorkOrderId == workOrderId);
        ApplyOrderByDescending(a => a.CreatedAt);
    }
}
