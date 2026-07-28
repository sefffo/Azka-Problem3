using Azka.Domain.Entities;
using Azka.Domain.Enums;
using System.Linq.Expressions;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Composable specification for the Work Order search endpoint (FR 7).
/// Every filter is optional — only non-null/non-empty values are applied.
/// </summary>
public class WorkOrderSearchSpecification : BaseSpecification<WorkOrder>
{
    // Paginated data query — includes, ordering, and pagination applied
    public WorkOrderSearchSpecification(
        string? workOrderNumber,
        string? assetNumber,
        WorkOrderStatus? status,
        Priority? priority,
        string? region,
        string? engineerName,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize)
    {
        AddInclude(w => w.Asset);
        AddInclude("Assignments.Engineer");

        AddCriteria(BuildCriteria(
            workOrderNumber, assetNumber, status, priority,
            region, engineerName, fromDate, toDate));

        ApplyOrderByDescending(w => (object)(int)w.Priority);
        ApplyPaging((page - 1) * pageSize, pageSize);
    }

    // Count-only query — same filters, no pagination, no includes (fast)
    public WorkOrderSearchSpecification(
        string? workOrderNumber,
        string? assetNumber,
        WorkOrderStatus? status,
        Priority? priority,
        string? region,
        string? engineerName,
        DateTime? fromDate,
        DateTime? toDate)
    {
        AddCriteria(BuildCriteria(
            workOrderNumber, assetNumber, status, priority,
            region, engineerName, fromDate, toDate));
    }

    private static Expression<Func<WorkOrder, bool>> BuildCriteria(
        string? workOrderNumber,
        string? assetNumber,
        WorkOrderStatus? status,
        Priority? priority,
        string? region,
        string? engineerName,
        DateTime? fromDate,
        DateTime? toDate)
    {
        return w =>
            (string.IsNullOrWhiteSpace(workOrderNumber) || w.WorkOrderNumber.Contains(workOrderNumber)) &&
            (string.IsNullOrWhiteSpace(assetNumber)     || w.Asset.AssetNumber.Contains(assetNumber)) &&
            (!status.HasValue                           || w.Status == status.Value) &&
            (!priority.HasValue                         || w.Priority == priority.Value) &&
            (string.IsNullOrWhiteSpace(region)          || w.Assignments.Any(a => a.Engineer.Region.ToLower() == region.ToLower())) &&
            (string.IsNullOrWhiteSpace(engineerName)    || w.Assignments.Any(a => a.Engineer.FullName.Contains(engineerName))) &&
            (!fromDate.HasValue                         || w.RequestedDate >= fromDate.Value) &&
            (!toDate.HasValue                           || w.RequestedDate <= toDate.Value);
    }
}
