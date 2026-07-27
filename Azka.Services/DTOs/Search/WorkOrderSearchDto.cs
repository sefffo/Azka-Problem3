using Azka.Domain.Enums;

namespace Azka.Services.DTOs.Search;

public class WorkOrderSearchDto
{
    public string? EngineerName { get; set; }
    public string? AssetNumber { get; set; }
    public string? Region { get; set; }
    public string? WorkOrderNumber { get; set; }
    public WorkOrderStatus? Status { get; set; }
    public Priority? Priority { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
