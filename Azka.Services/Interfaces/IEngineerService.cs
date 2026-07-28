using Azka.Services.DTOs.Engineer;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IEngineerService
{
    Task<ApiResponse<PagedResult<EngineerDto>>> GetAllAsync(EngineerQueryDto query);
    Task<ApiResponse<EngineerDto>>              GetByIdAsync(int id);
    Task<ApiResponse<EngineerDto>>              CreateAsync(CreateEngineerDto dto);
    Task<ApiResponse<EngineerDto>>              UpdateAsync(int id, UpdateEngineerDto dto);
    Task<ApiResponse<bool>>                     DeleteAsync(int id);
    Task<ApiResponse<EngineerWorkloadDto>>       GetWorkloadAsync(int id, DateTime date);
}
