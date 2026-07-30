using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assignments;
using Azka.Domain.Specifications.Engineers;
using Azka.Services.DTOs.Engineer;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.Extensions.Caching.Memory;

namespace Azka.Services.Implementation;

/// <summary>
/// Manages engineer profiles, real-time workload, and availability checks.
///
/// ════════════════════════════════════════════════════════════
///  GET ALL ENGINEERS FLOW  (cached)
/// ════════════════════════════════════════════════════════════
///
///  HTTP GET /api/Engineers?region=&team=&isActive=&page=
///       │
///       ▼
///  [EngineersController] ──► GetAllAsync(EngineerQueryDto)
///       │
///       ▼
///  IMemoryCache.TryGetValue(cacheKey)
///       │
///       ├─ [HIT] ──────────────────────────────────────────────────────────┐
///       │                                                                   │
///       ▼ [MISS]                                                            │
///  EngineerQuerySpecification (countOnly: true)                             │
///  EngineerRepository.CountAsync()         ← DB: Engineers COUNT           │
///       │                                                                   │
///  EngineerQuerySpecification (paged)                                       │
///  EngineerRepository.ListAsync()          ← DB: Engineers SELECT + filters│
///       │                                                                   │
///  Build PagedResult&lt;EngineerDto&gt;                                           │
///       │                                                                   │
///  IMemoryCache.Set(key, result, TTL=5min)                                  │
///       │                                                                   │
///       └──────────────────────────────────────────────────────────────────┘
///       │
///       ▼
///  HTTP 200 { items[], totalCount, page, pageSize }
///
/// ════════════════════════════════════════════════════════════
///  GET WORKLOAD FLOW  (real-time — never cached)
/// ════════════════════════════════════════════════════════════
///
///  HTTP GET /api/Engineers/{id}/workload?date=2026-07-30
///       │
///       ▼
///  EngineerRepository.GetByIdAsync(id)     ← DB: Engineers SELECT by PK
///       │
///       ├─ [not found] ──► NotFoundException 404
///       │
///       ▼
///  EngineerWorkloadSpecification(id, date)
///  AssignmentRepository.ListAsync()        ← DB: Assignments JOIN WorkOrders
///       │
///       ▼
///  Compute: totalHours = SUM(EstimatedHours)
///           remaining  = DailyCapacityHours − totalHours
///           utilization% = (totalHours / DailyCapacityHours) * 100
///       │
///       ▼
///  HTTP 200 EngineerWorkloadDto
///
/// ════════════════════════════════════════════════════════════
///  GET AVAILABLE ENGINEERS FLOW  (real-time — never cached)
/// ════════════════════════════════════════════════════════════
///
///  HTTP GET /api/Engineers/available?from=&to=&region=
///       │
///       ▼
///  Validate: to > from  (else Failure 400)
///       │
///       ▼
///  AvailableEngineersSpecification(region)
///  EngineerRepository.ListAsync()          ← DB: Engineers SELECT (active only)
///       │
///       ▼
///  For each engineer:
///   ├─ IsWithinWorkingHours(from, to)?      (pure time-string parse — no DB)
///   │   └─ [NO] skip
///   ├─ AssignmentConflictSpecification(id, from, to)
///   │   AssignmentRepository.AnyAsync()    ← DB: Assignments (conflict check)
///   │   └─ [conflict] skip
///   ├─ DailyCapacitySpecification(id, from.Date)
///   │   AssignmentRepository.ListAsync()   ← DB: Assignments + WorkOrders
///   │   currentLoad = SUM(EstimatedHours)
///   │   └─ [currentLoad + duration > capacity] skip
///   └─ append to available[]
///       │
///       ▼
///  HTTP 200 IReadOnlyList&lt;EngineerAvailabilityDto&gt;
///
/// ════════════════════════════════════════════════════════════
///  CREATE ENGINEER FLOW
/// ════════════════════════════════════════════════════════════
///
///  HTTP POST /api/Engineers  [Admin, Dispatcher]
///       │
///       ▼
///  EngineerDuplicateSpecification(employeeNumber)
///  EngineerRepository.CountAsync()         ← DB: Engineers COUNT
///       │
///       ├─ [duplicate] ──► ConflictException 409
///       │
///       ▼
///  Build Engineer entity  { IsActive = true }
///  EngineerRepository.AddAsync()           → DB: EF ChangeTracker (pending)
///  UnitOfWork.SaveChangesAsync()           → DB: Engineers INSERT
///       │
///       ▼
///  InvalidateEngineerCaches()
///   ├─ MemoryCache.Compact(0)              (clears all engineer list keys)
///   └─ IDashboardService.InvalidateDashboard()
///       │
///       ▼
///  HTTP 201 { engineer }
///
/// ════════════════════════════════════════════════════════════
///  UPDATE / DELETE (soft) FLOW  — same invalidation path
/// ════════════════════════════════════════════════════════════
///
///  HTTP PUT /api/Engineers/{id}  or  DELETE /api/Engineers/{id}
///       │
///       ▼
///  EngineerRepository.GetByIdAsync(id)     ← DB: Engineers SELECT by PK
///       ├─ [not found] ──► NotFoundException 404
///       ▼
///  Mutate fields / set IsActive=false
///  EngineerRepository.Update()             → DB: EF ChangeTracker (pending)
///  UnitOfWork.SaveChangesAsync()           → DB: Engineers UPDATE
///       │
///       ▼
///  InvalidateEngineerCaches()
///       │
///       ▼
///  HTTP 200 { engineer | success:true }
/// </summary>
public class EngineerService(
    IUnitOfWork unitOfWork,
    IMemoryCache cache,
    IDashboardService dashboardService) : IEngineerService
{
    // Engineer lists change infrequently — 5-minute TTL is safe.
    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromMinutes(5);

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<EngineerDto>>> GetAllAsync(EngineerQueryDto q)
    {
        // da lel cached values tab3an dayman fresh w new 3shan lw hasal ay post 3la el engineers bmsah el key al adym w bahot gdeed 
        var cacheKey = CacheKeys.EngineerListPrefix +
                       $"{q.Region}_{q.Team}_{q.IsActive}_{q.WorkingHours}_{q.Page}_{q.PageSize}";

        if (cache.TryGetValue(cacheKey, out PagedResult<EngineerDto>? cached))
            return ApiResponse<PagedResult<EngineerDto>>.Success(cached!);

        var repo = unitOfWork.GetRepository<Engineer, int>();

        var countSpec = new EngineerQuerySpecification(
            q.Region, q.Team, q.IsActive, q.WorkingHours, countOnly: true);
        var dataSpec  = new EngineerQuerySpecification(
            q.Region, q.Team, q.IsActive, q.WorkingHours, q.Page, q.PageSize);

        var total = await repo.CountAsync(countSpec);
        var items = await repo.ListAsync(dataSpec);

        var result = new PagedResult<EngineerDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = q.Page,
            PageSize   = q.PageSize
        };

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ListCacheTtl,
            Size = 1
        });

        return ApiResponse<PagedResult<EngineerDto>>.Success(result);
    }

    public async Task<ApiResponse<EngineerDto>> GetByIdAsync(int id)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);
        return ApiResponse<EngineerDto>.Success(MapToDto(engineer));
    }

    // ── Writes (all invalidate engineer list cache + dashboard cache) ────────

    public async Task<ApiResponse<EngineerDto>> CreateAsync(CreateEngineerDto dto)
    {
        var duplicateSpec = new EngineerDuplicateSpecification(dto.EmployeeNumber);
        if (await unitOfWork.GetRepository<Engineer, int>().CountAsync(duplicateSpec) > 0)
            throw new ConflictException($"Engineer with employee number '{dto.EmployeeNumber}' already exists.");

        var engineer = new Engineer
        {
            EmployeeNumber     = dto.EmployeeNumber,
            FullName           = dto.FullName,
            Email              = dto.Email,
            Team               = dto.Team,
            Region             = dto.Region,
            Skills             = dto.Skills,
            WorkingHours       = dto.WorkingHours,
            DailyCapacityHours = dto.DailyCapacityHours,
            IsActive           = true
        };

        await unitOfWork.GetRepository<Engineer, int>().AddAsync(engineer);
        await unitOfWork.SaveChangesAsync();

        InvalidateEngineerCaches();

        return ApiResponse<EngineerDto>.Success(MapToDto(engineer), "Engineer created successfully.");
    }
    //hnstakhdem patch 3shan msh bn3dl 3la kolo 
    public async Task<ApiResponse<EngineerDto>> UpdateAsync(int id, UpdateEngineerDto dto)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);

        engineer.FullName           = dto.FullName;
        engineer.Email              = dto.Email;
        engineer.Team               = dto.Team;
        engineer.Region             = dto.Region;
        engineer.Skills             = dto.Skills;
        engineer.WorkingHours       = dto.WorkingHours;
        engineer.DailyCapacityHours = dto.DailyCapacityHours;
        engineer.IsActive           = dto.IsActive;

        unitOfWork.GetRepository<Engineer, int>().Update(engineer);
        await unitOfWork.SaveChangesAsync();

        InvalidateEngineerCaches();

        return ApiResponse<EngineerDto>.Success(MapToDto(engineer), "Engineer updated successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);

        engineer.IsActive = false;
        unitOfWork.GetRepository<Engineer, int>().Update(engineer);
        await unitOfWork.SaveChangesAsync();

        InvalidateEngineerCaches();

        return ApiResponse<bool>.Success(true, "Engineer deactivated successfully.");
    }

    // ── Non-cached reads (real-time workload/availability) ───────────────────

    
    //msh hynf3 a3ml cache hena 3shan lazem ko request tegy b el avalabilty real time ==> maybe in the future we ill use SignalR
    public async Task<ApiResponse<EngineerWorkloadDto>> GetWorkloadAsync(int id, DateTime date)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);

        var workloadSpec      = new EngineerWorkloadSpecification(id, date);
        var activeAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(workloadSpec);

        var totalHours  = activeAssignments.Sum(a => a.WorkOrder.EstimatedHours);
        var remaining   = Math.Max(0, engineer.DailyCapacityHours - totalHours);
        var utilization = engineer.DailyCapacityHours > 0
            ? (totalHours / engineer.DailyCapacityHours) * 100 : 0;

        return ApiResponse<EngineerWorkloadDto>.Success(new EngineerWorkloadDto
        {
            EngineerId             = engineer.Id,
            FullName               = engineer.FullName,
            Region                 = engineer.Region,
            AssignedWorkOrders     = activeAssignments.Count,
            TotalEstimatedHours    = totalHours,
            DailyCapacityHours     = engineer.DailyCapacityHours,
            RemainingCapacityHours = remaining,
            UtilizationPercentage  = Math.Round(utilization, 2)
        });
    }

    public async Task<ApiResponse<IReadOnlyList<EngineerAvailabilityDto>>> GetAvailableAsync(
        DateTime from, DateTime to, string? region = null)
    {
        if (to <= from)
            return ApiResponse<IReadOnlyList<EngineerAvailabilityDto>>.Failure("End time must be after start time.");

        var available = new List<EngineerAvailabilityDto>();
        var engineers = await unitOfWork.GetRepository<Engineer, int>()
            .ListAsync(new AvailableEngineersSpecification(region));

        var durationHours = (to - from).TotalHours;

        foreach (var engineer in engineers)
        {
            if (!IsWithinWorkingHours(engineer.WorkingHours, from, to))
                continue;

            var conflictSpec = new AssignmentConflictSpecification(engineer.Id, from, to);
            if (await unitOfWork.GetRepository<Assignment, int>().AnyAsync(conflictSpec))
                continue;

            var capacitySpec   = new DailyCapacitySpecification(engineer.Id, from);
            var dayAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(capacitySpec);
            var currentLoad    = dayAssignments.Sum(a => a.WorkOrder.EstimatedHours);

            if (currentLoad + durationHours > engineer.DailyCapacityHours)
                continue;

            available.Add(new EngineerAvailabilityDto
            {
                EngineerId             = engineer.Id,
                FullName               = engineer.FullName,
                Team                   = engineer.Team,
                Region                 = engineer.Region,
                Skills                 = engineer.Skills,
                WorkingHours           = engineer.WorkingHours,
                DailyCapacityHours     = engineer.DailyCapacityHours,
                CurrentLoadHours       = currentLoad,
                RemainingCapacityHours = engineer.DailyCapacityHours - currentLoad
            });
        }

        return ApiResponse<IReadOnlyList<EngineerAvailabilityDto>>.Success(available);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void InvalidateEngineerCaches()
    {
        if (cache is MemoryCache mc)
            mc.Compact(0);

        cache.Remove(CacheKeys.EngineerListPrefix + "tag");
        dashboardService.InvalidateDashboard();
    }
    
    
    //very important function
    // as it helps in all the logic of auto assigning and conflict checks 
    /// <summary>
    ///  checks if the engineer is working within the given working hours
    /// mohma gedannnn!!!!
    /// </summary>
    /// <param name="workingHours"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
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

    private static EngineerDto MapToDto(Engineer e) => new()
    {
        Id                 = e.Id,
        EmployeeNumber     = e.EmployeeNumber,
        FullName           = e.FullName,
        Email              = e.Email,
        Team               = e.Team,
        Region             = e.Region,
        Skills             = e.Skills,
        WorkingHours       = e.WorkingHours,
        DailyCapacityHours = e.DailyCapacityHours,
        IsActive           = e.IsActive
    };
}
