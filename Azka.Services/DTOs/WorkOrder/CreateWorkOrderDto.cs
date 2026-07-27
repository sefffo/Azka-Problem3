using Azka.Domain.Enums;

namespace Azka.Services.DTOs.WorkOrder;

public class CreateWorkOrderDto
{
    public int AssetId { get; set; }
    public MaintenanceType MaintenanceType { get; set; }
    public Priority Priority { get; set; }
    public double EstimatedHours { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
}
