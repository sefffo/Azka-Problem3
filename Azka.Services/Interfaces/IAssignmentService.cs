using Azka.Services.DTOs.Assignment;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IAssignmentService
{
    Task<ApiResponse<AssignmentDto>> CreateAsync(CreateAssignmentDto dto, string assignedBy);
    Task<ApiResponse<AssignmentDto>> RescheduleAsync(int id, RescheduleAssignmentDto dto, string changedBy);
    Task<ApiResponse<AssignmentDto>> UpdateStatusAsync(int id, UpdateAssignmentStatusDto dto);
    Task<ApiResponse<IEnumerable<AssignmentDto>>> GetByEngineerAsync(int engineerId);
    Task<ApiResponse<IEnumerable<AssignmentDto>>> GetByWorkOrderAsync(int workOrderId);
    Task<ApiResponse<bool>> CancelAsync(int id, string cancelledBy);
}
