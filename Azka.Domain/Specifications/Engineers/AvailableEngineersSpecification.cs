using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Returns engineers who are active and have remaining daily capacity
/// for a given date. The capacity check is done in the service layer
/// after this spec narrows down the candidate list.
/// </summary>
public class AvailableEngineersSpecification : BaseSpecification<Engineer>
{
    public AvailableEngineersSpecification(string? region = null)
    {
        AddInclude("Assignments");
        AddCriteria(e => e.IsActive &&
            (region == null || e.Region.ToLower() == region.ToLower()));
        ApplyOrderBy(e => e.FullName);
    }
}
