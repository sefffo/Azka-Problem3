using System.Linq.Expressions;
using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Assets;

/// <summary>
/// Unified specification for GET /api/Assets.
/// All parameters are optional.
/// </summary>
public class AssetQuerySpecification : BaseSpecification<Asset>
{
    public AssetQuerySpecification(
        AssetType?   assetType    = null,
        AssetStatus? status       = null,
        string?      customerName = null,
        string?      assetNumber  = null,
        int          page         = 1,
        int          pageSize     = 20,
        bool         countOnly    = false)
    {
        Expression<Func<Asset, bool>>? predicate = null;

        if (assetType.HasValue)
            predicate = Combine(predicate, a => a.AssetType == assetType.Value);

        if (status.HasValue)
            predicate = Combine(predicate, a => a.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(customerName))
            predicate = Combine(predicate, a => a.CustomerName.Contains(customerName));

        if (!string.IsNullOrWhiteSpace(assetNumber))
            predicate = Combine(predicate, a => a.AssetNumber.Contains(assetNumber));

        if (predicate is not null)
            AddCriteria(predicate);

        if (!countOnly)
        {
            ApplyOrderBy(a => a.AssetNumber);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>>? left,
        Expression<Func<T, bool>> right)
    {
        if (left is null) return right;
        var param     = Expression.Parameter(typeof(T));
        var leftBody  = Expression.Invoke(left,  param);
        var rightBody = Expression.Invoke(right, param);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), param);
    }
}
