using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Returns all active engineers in a given region.
/// Case-insensitive match.
/// </summary>
public class EngineerByRegionSpecification : BaseSpecification<Engineer>
{
    public EngineerByRegionSpecification(string region)
    {
        AddCriteria(e => e.IsActive && e.Region.ToLower() == region.ToLower());
        ApplyOrderBy(e => e.FullName);
    }
}
