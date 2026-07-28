using Azka.Domain.Entities;

namespace Azka.Domain.Specifications.Assets;

/// <summary>
/// Finds a single asset by its unique AssetNumber.
/// Used by AssetService.GetByAssetNumberAsync() and duplicate checks.
/// </summary>
public class AssetByNumberSpecification : BaseSpecification<Asset>
{
    public AssetByNumberSpecification(string assetNumber)
    {
        AddCriteria(a => a.AssetNumber == assetNumber);
    }
}
