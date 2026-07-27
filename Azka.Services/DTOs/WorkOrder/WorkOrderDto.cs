using Azka.Domain.Enums;

namespace Azka.Services.DTOs.WorkOrder;

public class WorkOrderDto
{
    public int Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public int AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetAddress { get; set; } = string.Empty;
    public string MaintenanceType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
