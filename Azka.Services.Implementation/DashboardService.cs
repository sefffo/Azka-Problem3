using Azka.Domain.Enums;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Dashboard;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Azka.Services.Implementation;

public class DashboardService(AppDbContext context) : IDashboardService
{
    public async Task<ApiResponse<DashboardDto>> GetDashboardAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // ─── Engineer Summary ─────────────────────────────────────────────────
        var engineers = await context.Engineers
            .AsNoTracking()
            .Include(e => e.Assignments.Where(a =>
                a.ScheduledStart < tomorrow
                && a.ScheduledEnd > today
                && a.Status != AssignmentStatus.Cancelled
                && a.Status != AssignmentStatus.Failed))
            .ThenInclude(a => a.WorkOrder)
            .ToListAsync();

        int totalEngineers = engineers.Count;
        int inactiveEngineers = engineers.Count(e => !e.IsActive);

        var activeEngineers = engineers.Where(e => e.IsActive).ToList();

        int busyEngineers = activeEngineers.Count(e => e.Assignments.Any());
        int availableEngineers = activeEngineers.Count(e => !e.Assignments.Any());
        int overloaded = activeEngineers.Count(e =>
        {
            var dailyHours = e.Assignments.Sum(a => a.WorkOrder?.EstimatedHours ?? 0);
            return dailyHours > e.DailyCapacityHours;
        });

        // ─── Work Order Summary ───────────────────────────────────────────────
        var workOrders = await context.WorkOrders
            .AsNoTracking()
            .ToListAsync();

        var dashboard = new DashboardDto
        {
            EngineerSummary = new EngineerSummaryDto
            {
                TotalEngineers = totalEngineers,
                AvailableEngineers = availableEngineers,
                BusyEngineers = busyEngineers,
                InactiveEngineers = inactiveEngineers,
                OverloadedEngineers = overloaded
            },
            WorkOrderSummary = new WorkOrderSummaryDto
            {
                TotalWorkOrders = workOrders.Count,
                OpenWorkOrders = workOrders.Count(w => w.Status == WorkOrderStatus.Open),
                AssignedWorkOrders = workOrders.Count(w => w.Status == WorkOrderStatus.Assigned),
                InProgressWorkOrders = workOrders.Count(w => w.Status == WorkOrderStatus.InProgress),
                CompletedWorkOrders = workOrders.Count(w => w.Status == WorkOrderStatus.Completed),
                OverdueWorkOrders = workOrders.Count(w => w.DueDate < DateTime.UtcNow && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled),
                EmergencyRequests = workOrders.Count(w => w.Priority == Priority.Emergency),
                CancelledWorkOrders = workOrders.Count(w => w.Status == WorkOrderStatus.Cancelled)
            }
        };

        return ApiResponse<DashboardDto>.Success(dashboard);
    }
}
