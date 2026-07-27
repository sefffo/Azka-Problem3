using Azka.Domain.Enums;

namespace Azka.Services.DTOs.Asset;

public class AssetDto
{
    public int Id { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime InstallationDate { get; set; }
}
