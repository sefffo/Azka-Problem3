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
        if (assetType.HasValue)
            AddCriteria(a => a.AssetType == assetType.Value);

        if (status.HasValue)
            AddCriteria(a => a.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(customerName))
            AddCriteria(a => a.CustomerName.Contains(customerName));

        if (!string.IsNullOrWhiteSpace(assetNumber))
            AddCriteria(a => a.AssetNumber.Contains(assetNumber));

        if (!countOnly)
        {
            ApplyOrderBy(a => a.AssetNumber);
            ApplyPaging((page - 1) * pageSize, pageSize);
        }
    }
}
