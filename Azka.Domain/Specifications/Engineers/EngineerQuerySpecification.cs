using System.Linq.Expressions;
using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Unified specification for GET /api/Engineers.
/// Replaces ActiveEngineersSpecification + EngineerByRegionSpecification.
/// All parameters are optional — omit to get all engineers (paginated).
/// </summary>
public class EngineerQuerySpecification : BaseSpecification<Engineer>
{
    public EngineerQuerySpecification(
        string? region       = null,
        string? team         = null,
        bool?   isActive     = true,
        string? workingHours = null,
        int     page         = 1,
        int     pageSize     = 20,
        bool    countOnly    = false)
    {
        // Build a single combined predicate to avoid overwriting Criteria.
        Expression<Func<Engineer, bool>>? predicate = null;

        if (isActive.HasValue)
            predicate = Combine(predicate, e => e.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(region))
            predicate = Combine(predicate, e => e.Region == region);

        if (!string.IsNullOrWhiteSpace(team))
            predicate = Combine(predicate, e => e.Team == team);

        if (!string.IsNullOrWhiteSpace(workingHours))
            predicate = Combine(predicate, e => e.WorkingHours.Contains(workingHours));

        if (predicate is not null)
            AddCriteria(predicate);

        if (!countOnly)
        {
            ApplyOrderBy(e => e.FullName);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }

    // Combines two predicates with &&. Returns right if left is null.
    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>>? left,
        Expression<Func<T, bool>> right)
    {
        if (left is null) return right;
        var param    = Expression.Parameter(typeof(T));
        var leftBody  = Expression.Invoke(left,  param);
        var rightBody = Expression.Invoke(right, param);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), param);
    }
}
