using Azka.Domain.Entities;
using Azka.Domain.Enums;

namespace Azka.Domain.Specifications.Assets;

/// <summary>
/// Returns all active assets of a specific type.
/// </summary>
public class AssetsByTypeSpecification : BaseSpecification<Asset>
{
    public AssetsByTypeSpecification(AssetType type)
    {
        AddCriteria(a => a.AssetType == type && a.Status != AssetStatus.Decommissioned);
        ApplyOrderBy(a => a.AssetNumber);
    }
}
