using Azka.Domain.Enums;

namespace Azka.Services.DTOs.Engineer;

/// <summary>
/// Query parameters for GET /api/Engineers.
/// All fields are optional.
/// </summary>
public class EngineerQueryDto
{
    public string?   Region    { get; set; }
    public string?   Team      { get; set; }
    public bool?     IsActive  { get; set; } = true;   // default: active only
    public WorkingShift? WorkingHours { get; set; }

    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
