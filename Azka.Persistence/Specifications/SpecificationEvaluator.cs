using Azka.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Azka.Persistence.Specifications;

/// <summary>
/// Translates an ISpecification<T> into a fully-composed IQueryable<T>.
/// This is the only place that knows about EF Core — the domain and service
/// layers remain completely persistence-agnostic.
/// </summary>
public static class SpecificationEvaluator<T> where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
    {
        var query = inputQuery;

        // 1. Apply WHERE clause
        if (spec.Criteria is not null)
            query = query.Where(spec.Criteria);

        // 2. Apply typed Include expressions (compile-time safe)
        query = spec.Includes.Aggregate(query,
            (current, include) => current.Include(include));

        // 3. Apply string-based includes for ThenInclude chains
        query = spec.IncludeStrings.Aggregate(query,
            (current, include) => current.Include(include));

        // 4. Apply ordering
        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            query = query.OrderByDescending(spec.OrderByDescending);

        // 5. Apply pagination (always AFTER ordering)
        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }
}
