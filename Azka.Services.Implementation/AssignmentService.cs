using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assignments;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Azka.Services.Implementation;

public class AssignmentService(
    IUnitOfWork unitOfWork,
    AppDbContext context) : IAssignmentService
{
    public async Task<ApiResponse<AssignmentDto>> CreateAsync(CreateAssignmentDto dto, string assignedBy)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(dto.EngineerId)
            ?? throw new NotFoundException(nameof(Engineer), dto.EngineerId);

        if (!engineer.IsActive)
            throw new BadRequestException($"Engineer '{engineer.FullName}' is not active.");

        var workOrder = await unitOfWork.GetRepository<WorkOrder, int>().GetByIdAsync(dto.WorkOrderId)
            ?? throw new NotFoundException(nameof(WorkOrder), dto.WorkOrderId);

        if (workOrder.Status == WorkOrderStatus.Cancelled)
            throw new BadRequestException("Cannot assign a cancelled work order.");
        if (workOrder.Status == WorkOrderStatus.Completed)
            throw new BadRequestException("Cannot assign a completed work order.");

        // ─── FR 5: Conflict detection via spec ──────────────────────────────────
        var conflictSpec = new AssignmentConflictSpecification(dto.EngineerId, dto.ScheduledStart, dto.ScheduledEnd);
        if (await unitOfWork.GetRepository<Assignment, int>().CountAsync(conflictSpec) > 0)
            throw new ConflictException($"Engineer '{engineer.FullName}' has an overlapping assignment during the requested time slot.");

        // ─── Daily capacity check via spec ──────────────────────────────────
        var capacitySpec = new DailyCapacitySpecification(dto.EngineerId, dto.ScheduledStart);
        var dayAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(capacitySpec);
        var dailyHours = dayAssignments.Sum(a => a.WorkOrder.EstimatedHours);

        if (dailyHours + workOrder.EstimatedHours > engineer.DailyCapacityHours)
            throw new BadRequestException(
                $"Assigning this work order would exceed engineer '{engineer.FullName}'s daily capacity of {engineer.DailyCapacityHours}h.");

        // ─── Create ──────────────────────────────────────────────────────────
        var assignment = new Assignment
        {
            WorkOrderId    = dto.WorkOrderId,
            EngineerId     = dto.EngineerId,
            ScheduledStart = dto.ScheduledStart,
            ScheduledEnd   = dto.ScheduledEnd,
            Status         = AssignmentStatus.Assigned,
            AssignedBy     = assignedBy
        };

        await unitOfWork.GetRepository<Assignment, int>().AddAsync(assignment);

        workOrder.Status = WorkOrderStatus.Assigned;
        unitOfWork.GetRepository<WorkOrder, int>().Update(workOrder);
        await unitOfWork.SaveChangesAsync();

        assignment.Engineer  = engineer;
        assignment.WorkOrder = workOrder;
        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Assignment created successfully.");
    }

    public async Task<ApiResponse<AssignmentDto>> RescheduleAsync(int id, RescheduleAssignmentDto dto, string changedBy)
    {
        var assignment = await context.Assignments
            .Include(a => a.Engineer)
            .Include(a => a.WorkOrder)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException(nameof(Assignment), id);

        if (assignment.Status == AssignmentStatus.Completed)
            throw new BadRequestException("Completed assignments cannot be rescheduled.");
        if (assignment.Status == AssignmentStatus.Cancelled)
            throw new BadRequestException("Cancelled assignments cannot be rescheduled.");

        // ─── FR 6: Conflict check excluding self via spec ──────────────────────
        var conflictSpec = new AssignmentConflictSpecification(
            assignment.EngineerId, dto.NewScheduledStart, dto.NewScheduledEnd, excludeAssignmentId: id);
        if (await unitOfWork.GetRepository<Assignment, int>().CountAsync(conflictSpec) > 0)
            throw new ConflictException($"Engineer '{assignment.Engineer.FullName}' has a conflicting assignment during the new time slot.");

        // ─── Write history before mutation ──────────────────────────────────
        await unitOfWork.GetRepository<AssignmentHistory, int>().AddAsync(new AssignmentHistory
        {
            AssignmentId   = assignment.Id,
            PreviousStart  = assignment.ScheduledStart,
            PreviousEnd    = assignment.ScheduledEnd,
            PreviousStatus = assignment.Status.ToString(),
            ChangedBy      = changedBy,
            ChangeReason   = dto.ChangeReason
        });

        assignment.ScheduledStart = dto.NewScheduledStart;
        assignment.ScheduledEnd   = dto.NewScheduledEnd;
        assignment.UpdatedAt      = DateTime.UtcNow;

        unitOfWork.GetRepository<Assignment, int>().Update(assignment);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Assignment rescheduled successfully.");
    }

    public async Task<ApiResponse<AssignmentDto>> UpdateStatusAsync(int id, UpdateAssignmentStatusDto dto)
    {
        var assignment = await context.Assignments
            .Include(a => a.Engineer)
            .Include(a => a.WorkOrder)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException(nameof(Assignment), id);

        if (assignment.Status == AssignmentStatus.Completed)
            throw new BadRequestException("Completed assignments cannot be modified.");

        assignment.Status    = dto.Status;
        assignment.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == AssignmentStatus.InProgress)
            assignment.WorkOrder.Status = WorkOrderStatus.InProgress;
        else if (dto.Status == AssignmentStatus.Completed)
            assignment.WorkOrder.Status = WorkOrderStatus.Completed;

        unitOfWork.GetRepository<Assignment, int>().Update(assignment);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Status updated.");
    }

    public async Task<ApiResponse<IEnumerable<AssignmentDto>>> GetByEngineerAsync(int engineerId)
    {
        var spec = new AssignmentsByEngineerSpecification(engineerId);
        var assignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(spec);
        return ApiResponse<IEnumerable<AssignmentDto>>.Success(assignments.Select(MapToDto));
    }

    public async Task<ApiResponse<IEnumerable<AssignmentDto>>> GetByWorkOrderAsync(int workOrderId)
    {
        var spec = new AssignmentsByWorkOrderSpecification(workOrderId);
        var assignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(spec);
        return ApiResponse<IEnumerable<AssignmentDto>>.Success(assignments.Select(MapToDto));
    }

    public async Task<ApiResponse<bool>> CancelAsync(int id, string cancelledBy)
    {
        var assignment = await context.Assignments
            .Include(a => a.WorkOrder)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException(nameof(Assignment), id);

        if (assignment.Status == AssignmentStatus.Completed)
            throw new BadRequestException("Completed assignments cannot be cancelled.");

        await unitOfWork.GetRepository<AssignmentHistory, int>().AddAsync(new AssignmentHistory
        {
            AssignmentId   = assignment.Id,
            PreviousStart  = assignment.ScheduledStart,
            PreviousEnd    = assignment.ScheduledEnd,
            PreviousStatus = assignment.Status.ToString(),
            ChangedBy      = cancelledBy,
            ChangeReason   = "Cancelled"
        });

        assignment.Status            = AssignmentStatus.Cancelled;
        assignment.UpdatedAt         = DateTime.UtcNow;
        assignment.WorkOrder.Status  = WorkOrderStatus.PendingAssignment;

        unitOfWork.GetRepository<Assignment, int>().Update(assignment);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Assignment cancelled.");
    }

    private static AssignmentDto MapToDto(Assignment a) => new()
    {
        Id              = a.Id,
        WorkOrderId     = a.WorkOrderId,
        WorkOrderNumber = a.WorkOrder?.WorkOrderNumber ?? string.Empty,
        EngineerId      = a.EngineerId,
        EngineerName    = a.Engineer?.FullName ?? string.Empty,
        ScheduledStart  = a.ScheduledStart,
        ScheduledEnd    = a.ScheduledEnd,
        Status          = a.Status.ToString(),
        AssignedBy      = a.AssignedBy,
        CreatedAt       = a.CreatedAt
    };
}
