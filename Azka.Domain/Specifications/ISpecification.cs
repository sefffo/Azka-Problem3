using System.Linq.Expressions;

namespace Azka.Domain.Specifications;

/// <summary>
/// Core specification contract. Defines criteria, includes, ordering, and pagination
/// in a single, reusable, testable object — instead of scattering Where/Include/OrderBy
/// chains across service methods.
/// </summary>
public interface ISpecification<T>
{
    // Filter predicate — translated directly to SQL WHERE
    Expression<Func<T, bool>>? Criteria { get; }

    // Eager-load navigation properties — each becomes a SQL JOIN
    List<Expression<Func<T, object>>> Includes { get; }

    // String-based includes for ThenInclude chains (e.g. "Assignments.Engineer")
    List<string> IncludeStrings { get; }

    // Ordering
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }

    // Pagination
    int Take { get; }
    int Skip { get; }
    bool IsPagingEnabled { get; }
}
