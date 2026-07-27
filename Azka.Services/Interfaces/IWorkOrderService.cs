using Azka.Services.DTOs.Search;
using Azka.Services.DTOs.WorkOrder;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IWorkOrderService
{
    Task<ApiResponse<IEnumerable<WorkOrderDto>>> GetAllAsync();
    Task<ApiResponse<WorkOrderDto>> GetByIdAsync(int id);
    Task<ApiResponse<WorkOrderDto>> CreateAsync(CreateWorkOrderDto dto);
    Task<ApiResponse<WorkOrderDto>> UpdateStatusAsync(int id, UpdateWorkOrderStatusDto dto);
    Task<ApiResponse<bool>> CancelAsync(int id);
    Task<ApiResponse<PagedResult<WorkOrderDto>>> SearchAsync(WorkOrderSearchDto searchDto);
}
