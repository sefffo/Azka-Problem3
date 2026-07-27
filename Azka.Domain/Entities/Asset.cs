using Azka.Domain.Enums;

namespace Azka.Domain.Entities;

public class Asset : BaseEntity<int>
{
    public string AssetNumber { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public AssetStatus Status { get; set; } = AssetStatus.Active;
    public DateTime InstallationDate { get; set; }

    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
