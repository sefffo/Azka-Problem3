using Azka.Domain.Enums;

namespace Azka.Services.DTOs.Asset;

/// <summary>
/// Query parameters for GET /api/Assets.
/// All fields are optional.
/// </summary>
public class AssetQueryDto
{
    public AssetType?   AssetType    { get; set; }
    public AssetStatus? Status       { get; set; }
    public string?      CustomerName { get; set; }
    public string?      AssetNumber  { get; set; }

    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
