using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.WorkOrders;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Dashboard;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Azka.Services.Implementation;

/// <summary>
/// Aggregates real-time operational KPIs for the dashboard.
///
/// ════════════════════════════════════════════════════════════
///  GET DASHBOARD FLOW  (cached)
/// ════════════════════════════════════════════════════════════
///
///  HTTP GET /api/Dashboard
///       │
///       ▼
///  [DashboardController] ──► GetDashboardAsync()
///       │
///       ▼
///  IMemoryCache.TryGetValue(CacheKeys.Dashboard)
///       │
///       ├─ [HIT] ──────────────────────────────────────────────────────────┐
///       │                                                                   │
///       ▼ [MISS]                                                            │
///  BuildDashboardAsync()                                                    │
///   │                                                                       │
///   ├─ AppDbContext.Engineers                                               │
///   │   .Include(Assignments where active today)                           │
///   │   .ThenInclude(WorkOrder)                                             │
///   │   .AsNoTracking().ToListAsync()        ← DB: Engineers + Assignments  │
///   │                                             JOIN WorkOrders (1 query) │
///   │                                                                       │
///   ├─ WorkOrderRepository.CountAsync(All)   ← DB: WorkOrders COUNT        │
///   ├─ WorkOrderRepository.CountAsync(Open)  ← DB: WorkOrders COUNT        │
///   ├─ WorkOrderRepository.CountAsync(Assigned)                            │
///   ├─ WorkOrderRepository.CountAsync(InProgress)                          │
///   ├─ WorkOrderRepository.CountAsync(Completed)                           │
///   ├─ WorkOrderRepository.CountAsync(Cancelled)                           │
///   ├─ WorkOrderRepository.CountAsync(Overdue)                             │
///   └─ WorkOrderRepository.CountAsync(Emergency)  ← 8 lightweight COUNT    │
///       │                                            queries total          │
///       ▼                                                                   │
///  Build DashboardDto                                                       │
///   ├─ EngineerSummary: total / available / busy / overloaded / inactive   │
///   └─ WorkOrderSummary: all status counts + overdue + emergency           │
///       │                                                                   │
///  IMemoryCache.Set(Dashboard, dto, TTL=2min)                               │
///       │                                                                   │
///       └──────────────────────────────────────────────────────────────────┘
///       │
///       ▼
///  HTTP 200 DashboardDto
///
/// ════════════════════════════════════════════════════════════
///  INVALIDATE DASHBOARD  (called by other services on writes)
/// ════════════════════════════════════════════════════════════
///
///  EngineerService / AssetService / AssignmentService
///   └──► IDashboardService.InvalidateDashboard()
///             │
///             ▼
///        IMemoryCache.Remove(CacheKeys.Dashboard)
///             │
///             ▼
///        Next GET /api/Dashboard will trigger a full DB rebuild
/// </summary>
public class DashboardService(
    IUnitOfWork unitOfWork,
    AppDbContext context,
    IMemoryCache cache) : IDashboardService
{
    // Dashboard KPIs are expensive (9 DB queries) but tolerate 2-minute staleness.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<ApiResponse<DashboardDto>> GetDashboardAsync()
    {
        if (cache.TryGetValue(CacheKeys.Dashboard, out DashboardDto? cached))
            return ApiResponse<DashboardDto>.Success(cached!);

        var dto = await BuildDashboardAsync();

        cache.Set(CacheKeys.Dashboard, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            Size = 1
        });

        return ApiResponse<DashboardDto>.Success(dto);
    }

    /// <summary>
    /// Called by EngineerService, AssetService, and AssignmentService writes
    /// so the next dashboard request rebuilds from fresh data.
    /// </summary>
    public void InvalidateDashboard() => cache.Remove(CacheKeys.Dashboard);

    // ────────────────────────────────────────────────────────────────────────

    private async Task<DashboardDto> BuildDashboardAsync()
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

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

        var repo = unitOfWork.GetRepository<WorkOrder, int>();

        var totalWO      = await repo.CountAsync(new AllWorkOrdersSpecification());
        var openWO       = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Open));
        var assignedWO   = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Assigned));
        var inProgressWO = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.InProgress));
        var completedWO  = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Completed));
        var cancelledWO  = await repo.CountAsync(new WorkOrderByStatusSpecification(WorkOrderStatus.Cancelled));
        var overdueWO    = await repo.CountAsync(new OverdueWorkOrderSpecification());
        var emergencyWO  = await repo.CountAsync(new WorkOrderByPrioritySpecification(Priority.Emergency));

        return new DashboardDto
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
        };
    }
}
