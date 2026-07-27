using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
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
        var engineer = await context.Engineers.FindAsync(dto.EngineerId)
            ?? throw new NotFoundException(nameof(Engineer), dto.EngineerId);

        if (!engineer.IsActive)
            throw new BadRequestException($"Engineer '{engineer.FullName}' is not active.");

        var workOrder = await context.WorkOrders.FindAsync(dto.WorkOrderId)
            ?? throw new NotFoundException(nameof(WorkOrder), dto.WorkOrderId);

        if (workOrder.Status == WorkOrderStatus.Cancelled)
            throw new BadRequestException("Cannot assign a cancelled work order.");

        if (workOrder.Status == WorkOrderStatus.Completed)
            throw new BadRequestException("Cannot assign a completed work order.");

        // ─── Conflict Detection (Business Rule #1) ────────────────────────────
        var hasConflict = await context.Assignments
            .AnyAsync(a =>
                a.EngineerId == dto.EngineerId
                && a.Status != AssignmentStatus.Cancelled
                && a.Status != AssignmentStatus.Failed
                && a.ScheduledStart < dto.ScheduledEnd
                && a.ScheduledEnd > dto.ScheduledStart);

        if (hasConflict)
            throw new ConflictException(
                $"Engineer '{engineer.FullName}' has an overlapping assignment during the requested time slot.");

        // ─── Daily Capacity Check (Business Rule #7) ──────────────────────────
        var dayStart = dto.ScheduledStart.Date;
        var dayEnd = dayStart.AddDays(1);

        var dailyHours = await context.Assignments
            .AsNoTracking()
            .Include(a => a.WorkOrder)
            .Where(a => a.EngineerId == dto.EngineerId
                && a.ScheduledStart >= dayStart
                && a.ScheduledStart < dayEnd
                && a.Status != AssignmentStatus.Cancelled
                && a.Status != AssignmentStatus.Failed)
            .SumAsync(a => a.WorkOrder.EstimatedHours);

        if (dailyHours + workOrder.EstimatedHours > engineer.DailyCapacityHours)
            throw new BadRequestException(
                $"Assigning this work order would exceed engineer '{engineer.FullName}'s daily capacity of {engineer.DailyCapacityHours}h.");

        // ─── Create Assignment ────────────────────────────────────────────────
        var assignment = new Assignment
        {
            WorkOrderId = dto.WorkOrderId,
            EngineerId = dto.EngineerId,
            ScheduledStart = dto.ScheduledStart,
            ScheduledEnd = dto.ScheduledEnd,
            Status = AssignmentStatus.Assigned,
            AssignedBy = assignedBy
        };

        var repo = unitOfWork.GetRepository<Assignment, int>();
        await repo.AddAsync(assignment);

        // Update work order status to Assigned
        workOrder.Status = WorkOrderStatus.Assigned;
        var woRepo = unitOfWork.GetRepository<WorkOrder, int>();
        woRepo.Update(workOrder);

        await unitOfWork.SaveChangesAsync();

        assignment.Engineer = engineer;
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

        // ─── Conflict check excluding current assignment ───────────────────────
        var hasConflict = await context.Assignments
            .AnyAsync(a =>
                a.Id != id
                && a.EngineerId == assignment.EngineerId
                && a.Status != AssignmentStatus.Cancelled
                && a.Status != AssignmentStatus.Failed
                && a.ScheduledStart < dto.NewScheduledEnd
                && a.ScheduledEnd > dto.NewScheduledStart);

        if (hasConflict)
            throw new ConflictException(
                $"Engineer '{assignment.Engineer.FullName}' has a conflicting assignment during the new time slot.");

        // ─── Preserve history (Business Rule #6 & #9) ─────────────────────────
        var history = new AssignmentHistory
        {
            AssignmentId = assignment.Id,
            PreviousStart = assignment.ScheduledStart,
            PreviousEnd = assignment.ScheduledEnd,
            PreviousStatus = assignment.Status.ToString(),
            ChangedBy = changedBy,
            ChangeReason = dto.ChangeReason
        };

        var historyRepo = unitOfWork.GetRepository<AssignmentHistory, int>();
        await historyRepo.AddAsync(history);

        assignment.ScheduledStart = dto.NewScheduledStart;
        assignment.ScheduledEnd = dto.NewScheduledEnd;
        assignment.UpdatedAt = DateTime.UtcNow;

        var repo = unitOfWork.GetRepository<Assignment, int>();
        repo.Update(assignment);
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

        assignment.Status = dto.Status;
        assignment.UpdatedAt = DateTime.UtcNow;

        // Sync work order status
        if (dto.Status == AssignmentStatus.InProgress)
            assignment.WorkOrder.Status = WorkOrderStatus.InProgress;
        else if (dto.Status == AssignmentStatus.Completed)
            assignment.WorkOrder.Status = WorkOrderStatus.Completed;

        var repo = unitOfWork.GetRepository<Assignment, int>();
        repo.Update(assignment);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Status updated.");
    }

    public async Task<ApiResponse<IEnumerable<AssignmentDto>>> GetByEngineerAsync(int engineerId)
    {
        var assignments = await context.Assignments
            .AsNoTracking()
            .Include(a => a.Engineer)
            .Include(a => a.WorkOrder)
            .Where(a => a.EngineerId == engineerId)
            .OrderBy(a => a.ScheduledStart)
            .ToListAsync();

        return ApiResponse<IEnumerable<AssignmentDto>>.Success(assignments.Select(MapToDto));
    }

    public async Task<ApiResponse<IEnumerable<AssignmentDto>>> GetByWorkOrderAsync(int workOrderId)
    {
        var assignments = await context.Assignments
            .AsNoTracking()
            .Include(a => a.Engineer)
            .Include(a => a.WorkOrder)
            .Where(a => a.WorkOrderId == workOrderId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

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

        var history = new AssignmentHistory
        {
            AssignmentId = assignment.Id,
            PreviousStart = assignment.ScheduledStart,
            PreviousEnd = assignment.ScheduledEnd,
            PreviousStatus = assignment.Status.ToString(),
            ChangedBy = cancelledBy,
            ChangeReason = "Cancelled"
        };

        var historyRepo = unitOfWork.GetRepository<AssignmentHistory, int>();
        await historyRepo.AddAsync(history);

        assignment.Status = AssignmentStatus.Cancelled;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.WorkOrder.Status = WorkOrderStatus.PendingAssignment;

        var repo = unitOfWork.GetRepository<Assignment, int>();
        repo.Update(assignment);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Assignment cancelled.");
    }

    private static AssignmentDto MapToDto(Assignment a) => new()
    {
        Id = a.Id,
        WorkOrderId = a.WorkOrderId,
        WorkOrderNumber = a.WorkOrder?.WorkOrderNumber ?? string.Empty,
        EngineerId = a.EngineerId,
        EngineerName = a.Engineer?.FullName ?? string.Empty,
        ScheduledStart = a.ScheduledStart,
        ScheduledEnd = a.ScheduledEnd,
        Status = a.Status.ToString(),
        AssignedBy = a.AssignedBy,
        CreatedAt = a.CreatedAt
    };
}
