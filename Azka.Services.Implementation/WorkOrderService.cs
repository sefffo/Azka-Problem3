using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.WorkOrders;
using Azka.Persistence.Data;
using Azka.Services.DTOs.WorkOrder;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Azka.Services.Implementation;

public class WorkOrderService(
    IUnitOfWork unitOfWork,
    AppDbContext context) : IWorkOrderService
{
    /// <summary>
    /// Single query entry point — the controller builds a WorkOrderQueryDto
    /// and passes it in. No parameters = paginated list of everything.
    /// Replaces the old GetAllAsync() + SearchAsync() split.
    /// </summary>
    public async Task<ApiResponse<PagedResult<WorkOrderDto>>> GetAllAsync(WorkOrderQueryDto q)
    {
        var repo = unitOfWork.GetRepository<WorkOrder, int>();

        var countSpec = new WorkOrderQuerySpecification(
            q.WorkOrderNumber, q.AssetNumber, q.CustomerName,
            q.Status, q.Priority, q.FromDate, q.ToDate,
            countOnly: true);

        var dataSpec = new WorkOrderQuerySpecification(
            q.WorkOrderNumber, q.AssetNumber, q.CustomerName,
            q.Status, q.Priority, q.FromDate, q.ToDate,
            q.Page, q.PageSize);

        var total = await repo.CountAsync(countSpec);
        var items = await repo.ListAsync(dataSpec);

        return ApiResponse<PagedResult<WorkOrderDto>>.Success(new PagedResult<WorkOrderDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = q.Page,
            PageSize   = q.PageSize
        });
    }

    public async Task<ApiResponse<WorkOrderDto>> GetByIdAsync(int id)
    {
        var workOrder = await context.WorkOrders
            .Include(w => w.Asset)
            .FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new NotFoundException(nameof(WorkOrder), id);

        return ApiResponse<WorkOrderDto>.Success(MapToDto(workOrder));
    }

    public async Task<ApiResponse<WorkOrderDto>> CreateAsync(CreateWorkOrderDto dto)
    {
        var asset = await context.Assets.FindAsync(dto.AssetId)
            ?? throw new NotFoundException(nameof(Asset), dto.AssetId);

        var workOrderNumber = GenerateWorkOrderNumber(dto.MaintenanceType);

        var workOrder = new WorkOrder
        {
            WorkOrderNumber = workOrderNumber,
            AssetId         = dto.AssetId,
            MaintenanceType = dto.MaintenanceType,
            Priority        = dto.Priority,
            EstimatedHours  = dto.EstimatedHours,
            RequestedDate   = dto.RequestedDate,
            DueDate         = dto.DueDate,
            Notes           = dto.Notes,
            Status          = WorkOrderStatus.Open
        };

        var repo = unitOfWork.GetRepository<WorkOrder, int>();
        await repo.AddAsync(workOrder);
        await unitOfWork.SaveChangesAsync();

        workOrder.Asset = asset;
        return ApiResponse<WorkOrderDto>.Success(MapToDto(workOrder), "Work order created successfully.");
    }

    public async Task<ApiResponse<WorkOrderDto>> UpdateStatusAsync(int id, UpdateWorkOrderStatusDto dto)
    {
        var workOrder = await context.WorkOrders
            .Include(w => w.Asset)
            .FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new NotFoundException(nameof(WorkOrder), id);

        if (workOrder.Status == WorkOrderStatus.Completed)
            throw new BadRequestException("Completed work orders cannot be modified.");

        workOrder.Status = dto.Status;
        if (dto.Notes is not null) workOrder.Notes = dto.Notes;

        unitOfWork.GetRepository<WorkOrder, int>().Update(workOrder);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<WorkOrderDto>.Success(MapToDto(workOrder), "Status updated successfully.");
    }

    public async Task<ApiResponse<bool>> CancelAsync(int id)
    {
        var workOrder = await context.WorkOrders.FindAsync(id)
            ?? throw new NotFoundException(nameof(WorkOrder), id);

        if (workOrder.Status == WorkOrderStatus.Completed)
            throw new BadRequestException("Completed work orders cannot be cancelled.");

        workOrder.Status = WorkOrderStatus.Cancelled;
        unitOfWork.GetRepository<WorkOrder, int>().Update(workOrder);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Work order cancelled.");
    }

    private static string GenerateWorkOrderNumber(MaintenanceType type)
    {
        var prefix = type switch
        {
            MaintenanceType.PreventiveMaintenance => "PM",
            MaintenanceType.CorrectiveMaintenance => "CM",
            MaintenanceType.EmergencyMaintenance  => "EM",
            MaintenanceType.Installation          => "IN",
            MaintenanceType.Inspection            => "IS",
            _ => "WO"
        };
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }

    private static WorkOrderDto MapToDto(WorkOrder w) => new()
    {
        Id              = w.Id,
        WorkOrderNumber = w.WorkOrderNumber,
        AssetId         = w.AssetId,
        AssetNumber     = w.Asset?.AssetNumber ?? string.Empty,
        AssetAddress    = w.Asset?.Address ?? string.Empty,
        MaintenanceType = w.MaintenanceType.ToString(),
        Priority        = w.Priority.ToString(),
        EstimatedHours  = w.EstimatedHours,
        RequestedDate   = w.RequestedDate,
        DueDate         = w.DueDate,
        Status          = w.Status.ToString(),
        Notes           = w.Notes,
        CreatedAt       = w.CreatedAt
    };
}
