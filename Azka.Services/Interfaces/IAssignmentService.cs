using Azka.Services.DTOs.Assignment;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IAssignmentService
{
    Task<ApiResponse<PagedResult<AssignmentDto>>> GetAllAsync(AssignmentQueryDto query);
    Task<ApiResponse<AssignmentDto>>              GetByIdAsync(int id);
    Task<ApiResponse<AssignmentDto>>              CreateAsync(CreateAssignmentDto dto);
    Task<ApiResponse<AssignmentDto>>              UpdateStatusAsync(int id, UpdateAssignmentStatusDto dto);
    Task<ApiResponse<bool>>                       DeleteAsync(int id);
}
