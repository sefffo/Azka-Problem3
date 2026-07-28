using Azka.Domain.Entities;
using Azka.Domain.Enums;
using System.Linq.Expressions;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Composable specification for the Work Order search endpoint (FR 7).
/// Every filter is optional — only non-null/non-empty values are applied.
/// Handles: WorkOrderNumber, AssetNumber, Status, Priority, Region,
///          EngineerName, FromDate, ToDate, and pagination.
/// </summary>
public class WorkOrderSearchSpecification : BaseSpecification<WorkOrder>
{
    public WorkOrderSearchSpecification(
        string? workOrderNumber,
        string? assetNumber,
        WorkOrderStatus? status,
        WorkOrderPriority? priority,
        string? region,
        string? engineerName,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize)
    {
        // ── Includes ──────────────────────────────────────────────────────────
        AddInclude(w => w.Asset);
        AddInclude("Assignments.Engineer");  // ThenInclude chain

        // ── Build combined filter predicate ───────────────────────────────────
        AddCriteria(BuildCriteria(
            workOrderNumber, assetNumber, status, priority,
            region, engineerName, fromDate, toDate));

        // ── Ordering: Priority DESC then DueDate ASC ──────────────────────────
        ApplyOrderByDescending(w => (object)(int)w.Priority);

        // ── Pagination ────────────────────────────────────────────────────────
        ApplyPaging((page - 1) * pageSize, pageSize);
    }

    /// <summary>
    /// Separate count query — same filters, no pagination, no includes.
    /// Used to get the total before applying Skip/Take.
    /// </summary>
    public WorkOrderSearchSpecification(
        string? workOrderNumber,
        string? assetNumber,
        WorkOrderStatus? status,
        WorkOrderPriority? priority,
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
        WorkOrderPriority? priority,
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
