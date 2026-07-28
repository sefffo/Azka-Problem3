using System.Linq.Expressions;

namespace Azka.Domain.Specifications;

/// <summary>
/// Combines two specifications with a logical AND.
/// Usage: var spec = new WorkOrderByStatusSpec(status).And(new WorkOrderByPrioritySpec(priority));
/// </summary>
public class AndSpecification<T>(
    ISpecification<T> left,
    ISpecification<T> right) : BaseSpecification<T>
{
    public AndSpecification()
        : this(null!, null!)
    {
        // Required for derived classes that build their own criteria
    }

    private readonly ISpecification<T> _left = left;
    private readonly ISpecification<T> _right = right;

    public new Expression<Func<T, bool>>? Criteria
    {
        get
        {
            if (_left?.Criteria is null) return _right?.Criteria;
            if (_right?.Criteria is null) return _left.Criteria;

            var param = Expression.Parameter(typeof(T), "x");
            var leftBody = Expression.Invoke(_left.Criteria, param);
            var rightBody = Expression.Invoke(_right.Criteria, param);
            var andBody = Expression.AndAlso(leftBody, rightBody);
            return Expression.Lambda<Func<T, bool>>(andBody, param);
        }
    }
}
