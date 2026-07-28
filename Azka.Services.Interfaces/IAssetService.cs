using Azka.Services.DTOs.Asset;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IAssetService
{
    Task<ApiResponse<PagedResult<AssetDto>>> GetAllAsync(AssetQueryDto query);
    Task<ApiResponse<AssetDto>>              GetByIdAsync(int id);
    Task<ApiResponse<AssetDto>>              CreateAsync(CreateAssetDto dto);
    Task<ApiResponse<bool>>                  DeleteAsync(int id);
}
