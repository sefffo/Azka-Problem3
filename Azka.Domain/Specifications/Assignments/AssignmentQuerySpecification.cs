using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Assignments;

/// <summary>
/// Unified specification for GET /api/Assignments.
/// Replaces the separate GetByEngineerAsync / GetByWorkOrderAsync paths.
/// All parameters are optional.
/// </summary>
public class AssignmentQuerySpecification : BaseSpecification<Assignment>
{
    public AssignmentQuerySpecification(
        int?              engineerId  = null,
        int?              workOrderId = null,
        AssignmentStatus? status      = null,
        DateTime?         fromDate    = null,
        DateTime?         toDate      = null,
        int               page        = 1,
        int               pageSize    = 20,
        bool              countOnly   = false)
    {
        if (engineerId.HasValue)
            AddCriteria(a => a.EngineerId == engineerId.Value);

        if (workOrderId.HasValue)
            AddCriteria(a => a.WorkOrderId == workOrderId.Value);

        if (status.HasValue)
            AddCriteria(a => a.Status == status.Value);

        if (fromDate.HasValue)
            AddCriteria(a => a.ScheduledStart >= fromDate.Value);

        if (toDate.HasValue)
            AddCriteria(a => a.ScheduledEnd <= toDate.Value);

        if (!countOnly)
        {
            AddInclude(a => a.Engineer);
            AddInclude(a => a.WorkOrder);
            ApplyOrderByDescending(a => a.CreatedAt);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }
}
