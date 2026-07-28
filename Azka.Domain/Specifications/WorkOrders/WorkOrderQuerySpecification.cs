using System.Linq.Expressions;
using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Unified specification for GET /api/WorkOrders.
/// Merges the old WorkOrderSearchSpecification + AllWorkOrdersSpecification.
/// Note: 'region' filter removed — Asset has no Region property.
///       Filter by assetNumber or customerName instead.
/// </summary>
public class WorkOrderQuerySpecification : BaseSpecification<WorkOrder>
{
    public WorkOrderQuerySpecification(
        string?          workOrderNumber = null,
        string?          assetNumber     = null,
        string?          customerName    = null,
        WorkOrderStatus? status          = null,
        Priority?        priority        = null,
        DateTime?        fromDate        = null,
        DateTime?        toDate          = null,
        int              page            = 1,
        int              pageSize        = 20,
        bool             countOnly       = false)
    {
        Expression<Func<WorkOrder, bool>>? predicate = null;

        if (!string.IsNullOrWhiteSpace(workOrderNumber))
            predicate = Combine(predicate, w => w.WorkOrderNumber.Contains(workOrderNumber));

        if (!string.IsNullOrWhiteSpace(assetNumber))
            predicate = Combine(predicate, w => w.Asset != null && w.Asset.AssetNumber.Contains(assetNumber));

        if (!string.IsNullOrWhiteSpace(customerName))
            predicate = Combine(predicate, w => w.Asset != null && w.Asset.CustomerName.Contains(customerName));

        if (status.HasValue)
            predicate = Combine(predicate, w => w.Status == status.Value);

        if (priority.HasValue)
            predicate = Combine(predicate, w => w.Priority == priority.Value);

        if (fromDate.HasValue)
            predicate = Combine(predicate, w => w.RequestedDate >= fromDate.Value);

        if (toDate.HasValue)
            predicate = Combine(predicate, w => w.RequestedDate <= toDate.Value);

        if (predicate is not null)
            AddCriteria(predicate);

        if (!countOnly)
        {
            AddInclude(w => w.Asset);
            ApplyOrderByDescending(w => w.CreatedAt);
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
