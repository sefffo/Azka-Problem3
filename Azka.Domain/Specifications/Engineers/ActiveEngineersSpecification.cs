using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// All active engineers ordered by name.
/// Used by EngineerService.GetAllAsync().
/// </summary>
public class ActiveEngineersSpecification : BaseSpecification<Engineer>
{
    public ActiveEngineersSpecification()
    {
        AddCriteria(e => e.IsActive);
        ApplyOrderBy(e => e.FullName);
    }
}
