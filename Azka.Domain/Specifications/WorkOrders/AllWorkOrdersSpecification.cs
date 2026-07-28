using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.WorkOrders;

/// <summary>
/// Matches all work orders — used for total count on the dashboard.
/// No criteria means the SpecificationEvaluator skips the WHERE clause entirely.
/// </summary>
public class AllWorkOrdersSpecification : BaseSpecification<WorkOrder>
{
    public AllWorkOrdersSpecification()
    {
        // No criteria — intentional. Matches everything.
    }
}
