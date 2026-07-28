using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Retrieves all work orders matching a specific status.
/// Example: used by DashboardService to count open/in-progress/completed.
/// </summary>
public class WorkOrderByStatusSpecification : BaseSpecification<WorkOrder>
{
    public WorkOrderByStatusSpecification(WorkOrderStatus status)
    {
        AddCriteria(w => w.Status == status);
    }
}
