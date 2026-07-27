using Azka.Services.DTOs.Asset;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IAssetService
{
    Task<ApiResponse<IEnumerable<AssetDto>>> GetAllAsync();
    Task<ApiResponse<AssetDto>> GetByIdAsync(int id);
    Task<ApiResponse<AssetDto>> CreateAsync(CreateAssetDto dto);
    Task<ApiResponse<bool>> DeleteAsync(int id);
}
