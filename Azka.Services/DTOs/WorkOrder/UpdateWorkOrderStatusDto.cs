using Azka.Domain.Enums;

namespace Azka.Services.DTOs.WorkOrder;

public class UpdateWorkOrderStatusDto
{
    public WorkOrderStatus Status { get; set; }
    public string? Notes { get; set; }
}
