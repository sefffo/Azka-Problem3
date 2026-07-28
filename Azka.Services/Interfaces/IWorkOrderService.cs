using Azka.Services.DTOs.WorkOrder;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IWorkOrderService
{
    Task<ApiResponse<PagedResult<WorkOrderDto>>> GetAllAsync(WorkOrderQueryDto query);
    Task<ApiResponse<WorkOrderDto>>              GetByIdAsync(int id);
    Task<ApiResponse<WorkOrderDto>>              CreateAsync(CreateWorkOrderDto dto);
    Task<ApiResponse<WorkOrderDto>>              UpdateStatusAsync(int id, UpdateWorkOrderStatusDto dto);
    Task<ApiResponse<bool>>                      CancelAsync(int id);
}
