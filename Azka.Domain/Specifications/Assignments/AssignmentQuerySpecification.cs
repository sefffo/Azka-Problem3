using System.Linq.Expressions;
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
        int?              engineerId       = null,
        int?              workOrderId      = null,
        AssignmentStatus? status           = null,
        DateTime?         fromDate         = null,
        DateTime?         toDate           = null,
        int               page             = 1,
        int               pageSize         = 20,
        bool              countOnly        = false,
        bool              excludeCancelled = false)
    {
        Expression<Func<Assignment, bool>>? predicate = null;

        if (engineerId.HasValue)
            predicate = Combine(predicate, a => a.EngineerId == engineerId.Value);

        if (workOrderId.HasValue)
            predicate = Combine(predicate, a => a.WorkOrderId == workOrderId.Value);

        if (status.HasValue)
            predicate = Combine(predicate, a => a.Status == status.Value);

        if (excludeCancelled)
            predicate = Combine(predicate, a => a.Status != AssignmentStatus.Cancelled);

        if (fromDate.HasValue)
            predicate = Combine(predicate, a => a.ScheduledStart >= fromDate.Value);

        if (toDate.HasValue)
            predicate = Combine(predicate, a => a.ScheduledEnd <= toDate.Value);

        if (predicate is not null)
            AddCriteria(predicate);

        if (!countOnly)
        {
            AddInclude(a => a.Engineer);
            AddInclude(a => a.WorkOrder);
            ApplyOrderByDescending(a => a.CreatedAt);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>>? left,
        Expression<Func<T, bool>> right)
    {
        if (left is null) return right;
        var param     = Expression.Parameter(typeof(T));
        var leftBody  = Expression.Invoke(left,  param);
        var rightBody = Expression.Invoke(right, param);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), param);
    }
}
