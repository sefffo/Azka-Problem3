using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Unified specification for GET /api/WorkOrders.
/// Merges the old WorkOrderSearchSpecification + AllWorkOrdersSpecification:
/// - Pass all-null query params → returns everything (paginated).
/// - Pass any filter → narrows the result.
/// WorkOrdersController builds this spec and passes it straight to the repo.
/// </summary>
public class WorkOrderQuerySpecification : BaseSpecification<WorkOrder>
{
    public WorkOrderQuerySpecification(
        string?          workOrderNumber = null,
        string?          assetNumber     = null,
        WorkOrderStatus? status          = null,
        Priority?        priority        = null,
        string?          region          = null,
        string?          engineerName    = null,
        DateTime?        fromDate        = null,
        DateTime?        toDate          = null,
        int              page            = 1,
        int              pageSize        = 20,
        bool             countOnly       = false)
    {
        // ─── Optional filters ────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(workOrderNumber))
            AddCriteria(w => w.WorkOrderNumber.Contains(workOrderNumber));

        if (!string.IsNullOrWhiteSpace(assetNumber))
            AddCriteria(w => w.Asset != null && w.Asset.AssetNumber.Contains(assetNumber));

        if (status.HasValue)
            AddCriteria(w => w.Status == status.Value);

        if (priority.HasValue)
            AddCriteria(w => w.Priority == priority.Value);

        if (!string.IsNullOrWhiteSpace(region))
            AddCriteria(w => w.Asset != null && w.Asset.Region == region);

        if (fromDate.HasValue)
            AddCriteria(w => w.RequestedDate >= fromDate.Value);

        if (toDate.HasValue)
            AddCriteria(w => w.RequestedDate <= toDate.Value);

        // ─── Includes & ordering (skip for count queries) ────────────────────
        if (!countOnly)
        {
            AddInclude(w => w.Asset);
            ApplyOrderByDescending(w => w.CreatedAt);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }
}
