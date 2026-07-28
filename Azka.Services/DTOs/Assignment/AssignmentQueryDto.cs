using Azka.Domain.Enums;

namespace Azka.Services.DTOs.Assignment;

/// <summary>Query parameters for GET /api/Assignments. All fields are optional.</summary>
public class AssignmentQueryDto
{
    public int?              EngineerId  { get; set; }
    public int?              WorkOrderId { get; set; }
    public AssignmentStatus? Status      { get; set; }
    public DateTime?         FromDate    { get; set; }
    public DateTime?         ToDate      { get; set; }

    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
