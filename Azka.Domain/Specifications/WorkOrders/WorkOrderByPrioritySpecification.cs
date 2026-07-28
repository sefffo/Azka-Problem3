using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Retrieves all work orders matching a specific priority level.
/// Includes Asset for display. Ordered by DueDate ascending (most urgent first).
/// </summary>
public class WorkOrderByPrioritySpecification : BaseSpecification<WorkOrder>
{
    public WorkOrderByPrioritySpecification(WorkOrderPriority priority)
    {
        AddInclude(w => w.Asset);
        AddCriteria(w => w.Priority == priority);
        ApplyOrderBy(w => w.DueDate);
    }
}
