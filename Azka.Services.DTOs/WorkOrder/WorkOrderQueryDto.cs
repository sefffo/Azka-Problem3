using Azka.Domain.Enums;

namespace Azka.Services.DTOs.WorkOrder;

/// <summary>
/// Query parameters for GET /api/WorkOrders.
/// All fields are optional — omitting them means "no filter on that field".
/// Replaces the old WorkOrderSearchDto + GetAllAsync split.
/// </summary>
public class WorkOrderQueryDto
{
    public string?          WorkOrderNumber { get; set; }
    public string?          AssetNumber     { get; set; }
    public WorkOrderStatus? Status          { get; set; }
    public Priority?        Priority        { get; set; }
    public string?          Region          { get; set; }
    public string?          EngineerName    { get; set; }
    public DateTime?        FromDate        { get; set; }
    public DateTime?        ToDate          { get; set; }

    // Pagination — defaults to page 1, 20 per page
    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
