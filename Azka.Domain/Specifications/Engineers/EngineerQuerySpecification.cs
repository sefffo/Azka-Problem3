using System.Linq.Expressions;
using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Engineers;

/// <summary>
/// Unified specification for GET /api/Engineers.
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

    /// <summary>
    /// Combines two predicate expressions with AND using a parameter-replacement
    /// visitor so EF Core can translate the combined expression to SQL.
    /// (Expression.Invoke is NOT translatable by EF Core / SQL Server.)
    /// </summary>
    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>>? left,
        Expression<Func<T, bool>> right)
    {
        if (left is null) return right;

        // Reuse the parameter from 'left' inside the body of 'right'
        var param        = left.Parameters[0];
        var rightBody    = new ParameterReplacer(right.Parameters[0], param).Visit(right.Body);
        var combined     = Expression.AndAlso(left.Body, rightBody);
        return Expression.Lambda<Func<T, bool>>(combined, param);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
