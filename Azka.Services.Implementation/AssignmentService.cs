using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assignments;
using Azka.Domain.Specifications.Engineers;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Implementation.Email;
using Azka.Services.Interfaces;
using Azka.Shared.Common;

namespace Azka.Services.Implementation;

public class AssignmentService(
    IUnitOfWork unitOfWork,
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
        if (workOrder.Status == WorkOrderStatus.Assigned || workOrder.Status == WorkOrderStatus.InProgress)
            throw new BadRequestException($"Work order '{workOrder.WorkOrderNumber}' is already assigned.");

        var conflictSpec = new AssignmentConflictSpecification(dto.EngineerId, dto.ScheduledStart, dto.ScheduledEnd);
        if (await unitOfWork.GetRepository<Assignment, int>().AnyAsync(conflictSpec))
            throw new ConflictException($"Engineer '{engineer.FullName}' has an overlapping assignment during the requested time slot.");

        await CheckDailyCapacity(dto.EngineerId, dto.ScheduledStart, dto.ScheduledEnd, workOrder.EstimatedHours, engineer);

        await using var tx = await unitOfWork.BeginTransactionAsync();

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

        await tx.CommitAsync();

        assignment.Engineer  = engineer;
        assignment.WorkOrder = workOrder;

        if (!string.IsNullOrWhiteSpace(engineer.Email))
            await emailQueue.EnqueueAsync(new EmailJobDescriptor(
                To:      engineer.Email,
                Subject: $"New Assignment — Work Order {workOrder.WorkOrderNumber}",
                Body:    $"""
                         <h2>Hello {engineer.FullName},</h2>
                         <p>You have been assigned to <strong>Work Order {workOrder.WorkOrderNumber}</strong>.</p>
                         <ul>
                             <li><strong>Scheduled Start:</strong> {dto.ScheduledStart:f}</li>
                             <li><strong>Scheduled End:</strong> {dto.ScheduledEnd:f}</li>
                             <li><strong>Assigned by:</strong> {assignedBy}</li>
                         </ul>
                         <p>Please log in to the system for full details.</p>
                         <br/>
                         <p style="color:#888;font-size:12px;">Automated message from the Azka system.</p>
                         """));

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Assignment created successfully.");
    }

    public async Task<ApiResponse<AssignmentDto>> AutoAssignAsync(AutoAssignDto dto, string assignedBy)
    {
        var workOrder = await unitOfWork.GetRepository<WorkOrder, int>().GetByIdAsync(dto.WorkOrderId)
            ?? throw new NotFoundException(nameof(WorkOrder), dto.WorkOrderId);

        if (workOrder.Status == WorkOrderStatus.Cancelled)
            throw new BadRequestException("Cannot assign a cancelled work order.");
        if (workOrder.Status == WorkOrderStatus.Completed)
            throw new BadRequestException("Cannot assign a completed work order.");
        if (workOrder.Status == WorkOrderStatus.Assigned || workOrder.Status == WorkOrderStatus.InProgress)
            throw new BadRequestException($"Work order '{workOrder.WorkOrderNumber}' is already assigned.");

        var engineers = await unitOfWork.GetRepository<Engineer, int>()
            .ListAsync(new AvailableEngineersSpecification());

        var durationHours = (dto.ScheduledEnd - dto.ScheduledStart).TotalHours;
        Engineer? bestEngineer = null;
        double lowestLoad = double.MaxValue;

        foreach (var engineer in engineers)
        {
            if (!IsWithinWorkingHours(engineer.WorkingHours, dto.ScheduledStart, dto.ScheduledEnd))
                continue;

            var conflictSpec = new AssignmentConflictSpecification(engineer.Id, dto.ScheduledStart, dto.ScheduledEnd);
            if (await unitOfWork.GetRepository<Assignment, int>().AnyAsync(conflictSpec))
                continue;

            var capacitySpec = new DailyCapacitySpecification(engineer.Id, dto.ScheduledStart);
            var dayAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(capacitySpec);
            var currentLoad = dayAssignments.Sum(a => a.WorkOrder.EstimatedHours);

            if (currentLoad + durationHours > engineer.DailyCapacityHours)
                continue;

            if (currentLoad < lowestLoad)
            {
                lowestLoad = currentLoad;
                bestEngineer = engineer;
            }
        }

        if (bestEngineer is null)
            throw new ServiceUnavailableException("No available engineer found for the requested time slot.");

        await using var tx = await unitOfWork.BeginTransactionAsync();

        var assignment = new Assignment
        {
            WorkOrderId    = dto.WorkOrderId,
            EngineerId     = bestEngineer.Id,
            ScheduledStart = dto.ScheduledStart,
            ScheduledEnd   = dto.ScheduledEnd,
            Status         = AssignmentStatus.Assigned,
            AssignedBy     = assignedBy
        };

        await unitOfWork.GetRepository<Assignment, int>().AddAsync(assignment);

        workOrder.Status = WorkOrderStatus.Assigned;
        unitOfWork.GetRepository<WorkOrder, int>().Update(workOrder);
        await unitOfWork.SaveChangesAsync();

        await tx.CommitAsync();

        assignment.Engineer  = bestEngineer;
        assignment.WorkOrder = workOrder;
        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment),
            $"Auto-assigned to '{bestEngineer.FullName}' (load: {lowestLoad}h/{bestEngineer.DailyCapacityHours}h).");
    }

    private static bool IsWithinWorkingHours(string workingHours, DateTime start, DateTime end)
    {
        var parts = workingHours.Split('-');
        if (parts.Length != 2 ||
            !TimeOnly.TryParse(parts[0], out var whStart) ||
            !TimeOnly.TryParse(parts[1], out var whEnd))
            return false;

        var reqStart = TimeOnly.FromDateTime(start);
        var reqEnd   = TimeOnly.FromDateTime(end);
        return reqStart >= whStart && reqEnd <= whEnd;
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

        await using var tx = await unitOfWork.BeginTransactionAsync();
        var oldStart = assignment.ScheduledStart;
        var oldEnd   = assignment.ScheduledEnd;

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

        await tx.CommitAsync();
        if (!string.IsNullOrWhiteSpace(assignment.Engineer.Email))
            await emailQueue.EnqueueAsync(new EmailJobDescriptor(
                To:      assignment.Engineer.Email,
                Subject: $"Assignment Rescheduled — Work Order {assignment.WorkOrder.WorkOrderNumber}",
                Body:    $"""
                         <h2>Hello {assignment.Engineer.FullName},</h2>
                         <p>Your assignment for <strong>Work Order {assignment.WorkOrder.WorkOrderNumber}</strong> has been rescheduled.</p>
                         <table>
                             <tr><td><strong>Previous Start:</strong></td><td>{oldStart:f}</td></tr>
                             <tr><td><strong>Previous End:</strong></td><td>{oldEnd:f}</td></tr>
                             <tr><td><strong>New Start:</strong></td><td>{dto.NewScheduledStart:f}</td></tr>
                             <tr><td><strong>New End:</strong></td><td>{dto.NewScheduledEnd:f}</td></tr>
                             <tr><td><strong>Reason:</strong></td><td>{dto.ChangeReason}</td></tr>
                             <tr><td><strong>Changed by:</strong></td><td>{changedBy}</td></tr>
                         </table>
                         <br/>
                         <p style="color:#888;font-size:12px;">Automated message from the Azka system.</p>
                         """));

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Assignment rescheduled successfully.");
    }

    public async Task<ApiResponse<AssignmentDto>> UpdateStatusAsync(int id, UpdateAssignmentStatusDto dto)
    {
        var assignment = await unitOfWork.GetRepository<Assignment, int>()
            .GetBySpecAsync(new AssignmentByIdSpecification(id))
            ?? throw new NotFoundException(nameof(Assignment), id);

        if (assignment.Status == AssignmentStatus.Completed)
            throw new BadRequestException("Completed assignments cannot be modified.");

        var previousStatus = assignment.Status.ToString();

        assignment.Status    = dto.Status;
        assignment.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == AssignmentStatus.InProgress)
            assignment.WorkOrder.Status = WorkOrderStatus.InProgress;
        else if (dto.Status == AssignmentStatus.Completed)
            assignment.WorkOrder.Status = WorkOrderStatus.Completed;

        await unitOfWork.GetRepository<AssignmentHistory, int>().AddAsync(new AssignmentHistory
        {
            AssignmentId   = assignment.Id,
            PreviousStart  = assignment.ScheduledStart,
            PreviousEnd    = assignment.ScheduledEnd,
            PreviousStatus = previousStatus,
            ChangedBy      = "System",
            ChangeReason   = $"Status changed to {dto.Status}"
        });

        unitOfWork.GetRepository<Assignment, int>().Update(assignment);
        await unitOfWork.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(assignment.Engineer.Email)
            && dto.Status is AssignmentStatus.InProgress or AssignmentStatus.Completed)
            await emailQueue.EnqueueAsync(new EmailJobDescriptor(
                To:      assignment.Engineer.Email,
                Subject: $"Assignment Status Updated — Work Order {assignment.WorkOrder.WorkOrderNumber}",
                Body:    $"""
                         <h2>Hello {assignment.Engineer.FullName},</h2>
                         <p>The status of your assignment for <strong>Work Order {assignment.WorkOrder.WorkOrderNumber}</strong>
                         has been updated to <strong>{dto.Status}</strong>.</p>
                         <br/>
                         <p style="color:#888;font-size:12px;">Automated message from the Azka system.</p>
                         """));

        return ApiResponse<AssignmentDto>.Success(MapToDto(assignment), "Status updated.");
    }

    public async Task<ApiResponse<bool>> CancelAsync(int id, string cancelledBy)
    {
        var assignment = await unitOfWork.GetRepository<Assignment, int>()
            .GetBySpecAsync(new AssignmentByIdSpecification(id))
            ?? throw new NotFoundException(nameof(Assignment), id);

        if (assignment.Status == AssignmentStatus.Completed)
            throw new BadRequestException("Completed assignments cannot be cancelled.");

        var scheduledStart = assignment.ScheduledStart;
        var scheduledEnd   = assignment.ScheduledEnd;
        var woNumber       = assignment.WorkOrder.WorkOrderNumber;
        var engineerName   = assignment.Engineer.FullName;
        var engineerEmail  = assignment.Engineer.Email;

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

        if (!string.IsNullOrWhiteSpace(engineerEmail))
            await emailQueue.EnqueueAsync(new EmailJobDescriptor(
                To:      engineerEmail,
                Subject: $"Assignment Cancelled — Work Order {woNumber}",
                Body:    $"""
                         <h2>Hello {engineerName},</h2>
                         <p>Your assignment for <strong>Work Order {woNumber}</strong>
                         (scheduled {scheduledStart:f} → {scheduledEnd:f}) has been <strong>cancelled</strong>.</p>
                         <p><strong>Cancelled by:</strong> {cancelledBy}</p>
                         <br/>
                         <p style="color:#888;font-size:12px;">Automated message from the Azka system.</p>
                         """));

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

    private async Task CheckDailyCapacity(int engineerId, DateTime scheduledStart, DateTime scheduledEnd,
        double newHours, Engineer engineer)
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
