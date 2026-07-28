using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Unified specification for GET /api/Engineers.
/// Replaces ActiveEngineersSpecification + EngineerByRegionSpecification.
/// All parameters are optional — omit to get all engineers (paginated).
/// </summary>
public class EngineerQuerySpecification : BaseSpecification<Engineer>
{
    public EngineerQuerySpecification(
        string?       region       = null,
        string?       team         = null,
        bool?         isActive     = true,
        WorkingShift? workingHours = null,
        int           page         = 1,
        int           pageSize     = 20,
        bool          countOnly    = false)
    {
        if (isActive.HasValue)
            AddCriteria(e => e.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(region))
            AddCriteria(e => e.Region == region);

        if (!string.IsNullOrWhiteSpace(team))
            AddCriteria(e => e.Team == team);

        if (workingHours.HasValue)
            AddCriteria(e => e.WorkingHours == workingHours.Value);

        if (!countOnly)
        {
            ApplyOrderBy(e => e.FullName);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }
}
