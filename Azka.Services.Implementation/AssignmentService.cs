using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assignments;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Implementation.Email;
using Azka.Services.Interfaces;
using Azka.Shared.Common;

namespace Azka.Services.Implementation;

public class AssignmentService(
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    BackgroundEmailQueue emailQueue) : IAssignmentService
{
    public async Task<ApiResponse<PagedResult<AssignmentDto>>> GetAllAsync(AssignmentQueryDto q)
    {
        var repo = unitOfWork.GetRepository<Assignment, int>();

        var countSpec = new AssignmentQuerySpecification(
            q.EngineerId, q.WorkOrderId, q.Status, q.FromDate, q.ToDate, countOnly: true);

        var dataSpec = new AssignmentQuerySpecification(
            q.EngineerId, q.WorkOrderId, q.Status, q.FromDate, q.ToDate, q.Page, q.PageSize);

        var total = await repo.CountAsync(countSpec);
        var items = await repo.ListAsync(dataSpec);

        return ApiResponse<PagedResult<AssignmentDto>>.Success(new PagedResult<AssignmentDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = q.Page,
            PageSize   = q.PageSize
        });
    }

    public async Task<ApiResponse<AssignmentDto>> GetByIdAsync(int id)
    {
        var assignment = await unitOfWork.GetRepository<Assignment, int>()
            .GetBySpecAsync(new AssignmentByIdSpecification(id))
            ?? throw new NotFoundException(nameof(Assignment), id);

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment));
    }

    public async Task<ApiResponse<AssignmentDto>> CreateAsync(CreateAssignmentDto dto, string assignedBy)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(dto.EngineerId)
            ?? throw new NotFoundException(nameof(Engineer), dto.EngineerId);

        if (!engineer.IsActive)
            throw new BadRequestException($"Engineer '{engineer.FullName}' is not active.");

        ValidateWorkingHours(engineer.WorkingHours, dto.ScheduledStart, dto.ScheduledEnd, engineer.FullName);

        var workOrder = await unitOfWork.GetRepository<WorkOrder, int>().GetByIdAsync(dto.WorkOrderId)
            ?? throw new NotFoundException(nameof(WorkOrder), dto.WorkOrderId);

        if (workOrder.Status == WorkOrderStatus.Cancelled)
            throw new BadRequestException("Cannot assign a cancelled work order.");
        if (workOrder.Status == WorkOrderStatus.Completed)
            throw new BadRequestException("Cannot assign a completed work order.");

        var conflictSpec = new AssignmentConflictSpecification(dto.EngineerId, dto.ScheduledStart, dto.ScheduledEnd);
        if (await unitOfWork.GetRepository<Assignment, int>().AnyAsync(conflictSpec))
            throw new ConflictException($"Engineer '{engineer.FullName}' has an overlapping assignment during the requested time slot.");

        await CheckDailyCapacity(dto.EngineerId, dto.ScheduledStart, dto.ScheduledEnd, workOrder.EstimatedHours, engineer);

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

        // Notify engineer — fire-and-forget
        if (!string.IsNullOrWhiteSpace(engineer.Email))
        {
            var engineerName  = engineer.FullName;
            var engineerEmail = engineer.Email;
            var woNumber      = workOrder.WorkOrderNumber;
            var start         = dto.ScheduledStart.ToString("f");
            var end           = dto.ScheduledEnd.ToString("f");
            await emailQueue.EnqueueAsync(ct => emailService.SendAsync(
                to: engineerEmail,
                subject: $"New Assignment — Work Order {woNumber}",
                body: $"""
                    <h2>Hello {engineerName},</h2>
                    <p>You have been assigned to <strong>Work Order {woNumber}</strong>.</p>
                    <ul>
                        <li><strong>Scheduled Start:</strong> {start}</li>
                        <li><strong>Scheduled End:</strong> {end}</li>
                        <li><strong>Assigned by:</strong> {assignedBy}</li>
                    </ul>
                    <p>Please log in to the system for full details.</p>
                    <br/>
                    <p style="color:#888;font-size:12px;">This is an automated message from the Azka system.</p>
                    """));
        }

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Assignment created successfully.");
    }

    public async Task<ApiResponse<AssignmentDto>> RescheduleAsync(int id, RescheduleAssignmentDto dto, string changedBy)
    {
        var assignment = await unitOfWork.GetRepository<Assignment, int>()
            .GetBySpecAsync(new AssignmentByIdSpecification(id))
            ?? throw new NotFoundException(nameof(Assignment), id);

        if (assignment.Status == AssignmentStatus.Completed)
            throw new BadRequestException("Completed assignments cannot be rescheduled.");
        if (assignment.Status == AssignmentStatus.Cancelled)
            throw new BadRequestException("Cancelled assignments cannot be rescheduled.");

        ValidateWorkingHours(assignment.Engineer.WorkingHours, dto.NewScheduledStart, dto.NewScheduledEnd, assignment.Engineer.FullName);

        var conflictSpec = new AssignmentConflictSpecification(
            assignment.EngineerId, dto.NewScheduledStart, dto.NewScheduledEnd, excludeAssignmentId: id);
        if (await unitOfWork.GetRepository<Assignment, int>().AnyAsync(conflictSpec))
            throw new ConflictException($"Engineer '{assignment.Engineer.FullName}' has a conflicting assignment during the new time slot.");

        var newDurationHours = (dto.NewScheduledEnd - dto.NewScheduledStart).TotalHours;
        await CheckDailyCapacity(assignment.EngineerId, dto.NewScheduledStart, dto.NewScheduledEnd, newDurationHours, assignment.Engineer);

        await unitOfWork.GetRepository<AssignmentHistory, int>().AddAsync(new AssignmentHistory
        {
            AssignmentId   = assignment.Id,
            PreviousStart  = assignment.ScheduledStart,
            PreviousEnd    = assignment.ScheduledEnd,
            PreviousStatus = assignment.Status.ToString(),
            ChangedBy      = changedBy,
            ChangeReason   = dto.ChangeReason
        });

        var oldStart = assignment.ScheduledStart.ToString("f");
        var oldEnd   = assignment.ScheduledEnd.ToString("f");

        assignment.ScheduledStart = dto.NewScheduledStart;
        assignment.ScheduledEnd   = dto.NewScheduledEnd;
        assignment.UpdatedAt      = DateTime.UtcNow;

        unitOfWork.GetRepository<Assignment, int>().Update(assignment);
        await unitOfWork.SaveChangesAsync();

        // Notify engineer of reschedule — fire-and-forget
        if (!string.IsNullOrWhiteSpace(assignment.Engineer.Email))
        {
            var engineerName  = assignment.Engineer.FullName;
            var engineerEmail = assignment.Engineer.Email;
            var woNumber      = assignment.WorkOrder.WorkOrderNumber;
            var newStart      = dto.NewScheduledStart.ToString("f");
            var newEnd        = dto.NewScheduledEnd.ToString("f");
            var reason        = dto.ChangeReason;
            await emailQueue.EnqueueAsync(ct => emailService.SendAsync(
                to: engineerEmail,
                subject: $"Assignment Rescheduled — Work Order {woNumber}",
                body: $"""
                    <h2>Hello {engineerName},</h2>
                    <p>Your assignment for <strong>Work Order {woNumber}</strong> has been rescheduled.</p>
                    <table>
                        <tr><td><strong>Previous Start:</strong></td><td>{oldStart}</td></tr>
                        <tr><td><strong>Previous End:</strong></td><td>{oldEnd}</td></tr>
                        <tr><td><strong>New Start:</strong></td><td>{newStart}</td></tr>
                        <tr><td><strong>New End:</strong></td><td>{newEnd}</td></tr>
                        <tr><td><strong>Reason:</strong></td><td>{reason}</td></tr>
                        <tr><td><strong>Changed by:</strong></td><td>{changedBy}</td></tr>
                    </table>
                    <br/>
                    <p style="color:#888;font-size:12px;">This is an automated message from the Azka system.</p>
                    """));
        }

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Assignment rescheduled successfully.");
    }

    public async Task<ApiResponse<AssignmentDto>> UpdateStatusAsync(int id, UpdateAssignmentStatusDto dto)
    {
        var assignment = await unitOfWork.GetRepository<Assignment, int>()
            .GetBySpecAsync(new AssignmentByIdSpecification(id))
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

        // Notify engineer of status change — fire-and-forget
        if (!string.IsNullOrWhiteSpace(assignment.Engineer.Email)
            && dto.Status is AssignmentStatus.InProgress or AssignmentStatus.Completed)
        {
            var engineerName  = assignment.Engineer.FullName;
            var engineerEmail = assignment.Engineer.Email;
            var woNumber      = assignment.WorkOrder.WorkOrderNumber;
            var statusLabel   = dto.Status.ToString();
            await emailQueue.EnqueueAsync(ct => emailService.SendAsync(
                to: engineerEmail,
                subject: $"Assignment Status Updated — Work Order {woNumber}",
                body: $"""
                    <h2>Hello {engineerName},</h2>
                    <p>The status of your assignment for <strong>Work Order {woNumber}</strong> has been updated to <strong>{statusLabel}</strong>.</p>
                    <br/>
                    <p style="color:#888;font-size:12px;">This is an automated message from the Azka system.</p>
                    """));
        }

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Status updated.");
    }

    public async Task<ApiResponse<bool>> CancelAsync(int id, string cancelledBy)
    {
        var assignment = await unitOfWork.GetRepository<Assignment, int>()
            .GetBySpecAsync(new AssignmentByIdSpecification(id))
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

        assignment.Status           = AssignmentStatus.Cancelled;
        assignment.UpdatedAt        = DateTime.UtcNow;
        assignment.WorkOrder.Status = WorkOrderStatus.PendingAssignment;

        unitOfWork.GetRepository<Assignment, int>().Update(assignment);
        await unitOfWork.SaveChangesAsync();

        // Notify engineer of cancellation — fire-and-forget
        if (!string.IsNullOrWhiteSpace(assignment.Engineer.Email))
        {
            var engineerName  = assignment.Engineer.FullName;
            var engineerEmail = assignment.Engineer.Email;
            var woNumber      = assignment.WorkOrder.WorkOrderNumber;
            var start         = assignment.ScheduledStart.ToString("f");
            var end           = assignment.ScheduledEnd.ToString("f");
            await emailQueue.EnqueueAsync(ct => emailService.SendAsync(
                to: engineerEmail,
                subject: $"Assignment Cancelled — Work Order {woNumber}",
                body: $"""
                    <h2>Hello {engineerName},</h2>
                    <p>Your assignment for <strong>Work Order {woNumber}</strong> (scheduled {start} → {end}) has been <strong>cancelled</strong>.</p>
                    <p><strong>Cancelled by:</strong> {cancelledBy}</p>
                    <br/>
                    <p style="color:#888;font-size:12px;">This is an automated message from the Azka system.</p>
                    """));
        }

        return ApiResponse<bool>.Success(true, "Assignment cancelled.");
    }

    private static void ValidateWorkingHours(string workingHours, DateTime start, DateTime end, string engineerName)
    {
        var parts = workingHours.Split('-');
        if (parts.Length != 2 ||
            !TimeOnly.TryParse(parts[0], out var startWh) ||
            !TimeOnly.TryParse(parts[1], out var endWh))
            return;

        var startTime = TimeOnly.FromDateTime(start);
        var endTime   = TimeOnly.FromDateTime(end);

        if (startTime < startWh || endTime > endWh || endTime <= startWh)
            throw new BadRequestException(
                $"Scheduled time {startTime}-{endTime} falls outside engineer '{engineerName}'s working hours ({workingHours}).");
    }

    private async Task CheckDailyCapacity(int engineerId, DateTime scheduledStart, DateTime scheduledEnd, double newHours, Engineer engineer)
    {
        var capacitySpec   = new DailyCapacitySpecification(engineerId, scheduledStart);
        var dayAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(capacitySpec);
        var existingHours  = dayAssignments.Sum(a => a.WorkOrder.EstimatedHours);

        if (existingHours + newHours > engineer.DailyCapacityHours)
            throw new BadRequestException(
                $"Assigning this work order would exceed engineer '{engineer.FullName}'s daily capacity of {engineer.DailyCapacityHours}h.");
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
