using Azka.Services.DTOs.Assignment;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IAssignmentService
{
    Task<ApiResponse<PagedResult<AssignmentDto>>> GetAllAsync(AssignmentQueryDto query);
    Task<ApiResponse<AssignmentDto>>              CreateAsync(CreateAssignmentDto dto, string assignedBy);
    Task<ApiResponse<AssignmentDto>>              RescheduleAsync(int id, RescheduleAssignmentDto dto, string changedBy);
    Task<ApiResponse<AssignmentDto>>              UpdateStatusAsync(int id, UpdateAssignmentStatusDto dto);
    Task<ApiResponse<bool>>                       CancelAsync(int id, string cancelledBy);
}
