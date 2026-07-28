using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Finds all work orders that are past their due date and still not completed/cancelled.
/// Used by the dashboard's overdue count KPI and alert lists.
/// </summary>
public class OverdueWorkOrderSpecification : BaseSpecification<WorkOrder>
{
    public OverdueWorkOrderSpecification()
    {
        var now = DateTime.UtcNow;
        AddInclude(w => w.Asset);
        AddCriteria(w =>
            w.DueDate < now &&
            w.Status != WorkOrderStatus.Completed &&
            w.Status != WorkOrderStatus.Cancelled);
        ApplyOrderBy(w => w.DueDate);
    }
}
