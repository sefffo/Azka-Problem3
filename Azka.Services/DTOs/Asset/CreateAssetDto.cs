using Azka.Domain.Enums;

namespace Azka.Services.DTOs.Asset;

public class CreateAssetDto
{
    public string AssetNumber { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime InstallationDate { get; set; }
}
