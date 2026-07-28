using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.WorkOrders;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Dashboard;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Azka.Services.Implementation;

public class DashboardService(
    IUnitOfWork unitOfWork,
    AppDbContext context) : IDashboardService
{
    public async Task<ApiResponse<DashboardDto>> GetDashboardAsync()
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // ─── Engineer summary ───────────────────────────────────────────────────────
        // Filtered Include (today's active assignments only) — kept as EF
        // query because BaseSpecification doesn’t support filtered collections.
        var engineers = await context.Engineers
            .AsNoTracking()
            .Include(e => e.Assignments.Where(a =>
                a.ScheduledStart < tomorrow &&
                a.ScheduledEnd   > today    &&
                a.Status != AssignmentStatus.Cancelled &&
                a.Status != AssignmentStatus.Failed))
            .ThenInclude(a => a.WorkOrder)
            .ToListAsync();

        var activeEngineers = engineers.Where(e => e.IsActive).ToList();

        int overloaded = activeEngineers.Count(e =>
            e.Assignments.Sum(a => a.WorkOrder?.EstimatedHours ?? 0) > e.DailyCapacityHours);

        // ─── Work order KPIs via specs (each becomes a lean COUNT query) ────
        var repo = unitOfWork.GetRepository<WorkOrder, int>();

        var totalWO     = await repo.CountAsync(new AllWorkOrdersSpecification());
        var openWO      = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Open));
        var assignedWO  = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Assigned));
        var inProgressWO= await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.InProgress));
        var completedWO = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Completed));
        var cancelledWO = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Cancelled));
        var overdueWO   = await repo.CountAsync(new OverdueWorkOrderSpecification());
        var emergencyWO = await repo.CountAsync(new WorkOrderByPrioritySpecification(Priority.Emergency));

        return ApiResponse<DashboardDto>.Success(new DashboardDto
        {
            EngineerSummary = new EngineerSummaryDto
            {
                TotalEngineers      = engineers.Count,
                AvailableEngineers  = activeEngineers.Count(e => !e.Assignments.Any()),
                BusyEngineers       = activeEngineers.Count(e => e.Assignments.Any()),
                InactiveEngineers   = engineers.Count(e => !e.IsActive),
                OverloadedEngineers = overloaded
            },
            WorkOrderSummary = new WorkOrderSummaryDto
            {
                TotalWorkOrders      = totalWO,
                OpenWorkOrders       = openWO,
                AssignedWorkOrders   = assignedWO,
                InProgressWorkOrders = inProgressWO,
                CompletedWorkOrders  = completedWO,
                OverdueWorkOrders    = overdueWO,
                EmergencyRequests    = emergencyWO,
                CancelledWorkOrders  = cancelledWO
            }
        });
    }
}
