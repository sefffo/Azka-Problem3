using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assignments;
using Azka.Domain.Specifications.Engineers;
using Azka.Services.DTOs.Engineer;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;

namespace Azka.Services.Implementation;

public class EngineerService(
    IUnitOfWork unitOfWork) : IEngineerService
{
    public async Task<ApiResponse<PagedResult<EngineerDto>>> GetAllAsync(EngineerQueryDto q)
    {
        var repo = unitOfWork.GetRepository<Engineer, int>();

        var countSpec = new EngineerQuerySpecification(
            q.Region, q.Team, q.IsActive, q.WorkingHours, countOnly: true);

        var dataSpec = new EngineerQuerySpecification(
            q.Region, q.Team, q.IsActive, q.WorkingHours, q.Page, q.PageSize);

        var total = await repo.CountAsync(countSpec);
        var items = await repo.ListAsync(dataSpec);

        return ApiResponse<PagedResult<EngineerDto>>.Success(new PagedResult<EngineerDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = q.Page,
            PageSize   = q.PageSize
        });
    }

    public async Task<ApiResponse<EngineerDto>> GetByIdAsync(int id)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);
        return ApiResponse<EngineerDto>.Success(MapToDto(engineer));
    }

    public async Task<ApiResponse<EngineerDto>> CreateAsync(CreateEngineerDto dto)
    {
        var duplicateSpec = new EngineerDuplicateSpecification(dto.EmployeeNumber);
        if (await unitOfWork.GetRepository<Engineer, int>().CountAsync(duplicateSpec) > 0)
            throw new ConflictException($"Engineer with employee number '{dto.EmployeeNumber}' already exists.");

        var engineer = new Engineer
        {
            EmployeeNumber     = dto.EmployeeNumber,
            FullName           = dto.FullName,
            Team               = dto.Team,
            Region             = dto.Region,
            Skills             = dto.Skills,
            WorkingHours       = dto.WorkingHours,
            DailyCapacityHours = dto.DailyCapacityHours,
            IsActive           = true
        };

        await unitOfWork.GetRepository<Engineer, int>().AddAsync(engineer);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<EngineerDto>.Success(MapToDto(engineer), "Engineer created successfully.");
    }

    public async Task<ApiResponse<EngineerDto>> UpdateAsync(int id, UpdateEngineerDto dto)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);

        engineer.FullName           = dto.FullName;
        engineer.Team               = dto.Team;
        engineer.Region             = dto.Region;
        engineer.Skills             = dto.Skills;
        engineer.WorkingHours       = dto.WorkingHours;
        engineer.DailyCapacityHours = dto.DailyCapacityHours;
        engineer.IsActive           = dto.IsActive;

        unitOfWork.GetRepository<Engineer, int>().Update(engineer);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<EngineerDto>.Success(MapToDto(engineer), "Engineer updated successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);

        engineer.IsActive = false;
        unitOfWork.GetRepository<Engineer, int>().Update(engineer);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Engineer deactivated successfully.");
    }

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

    public async Task<ApiResponse<IReadOnlyList<EngineerAvailabilityDto>>> GetAvailableAsync(DateTime from, DateTime to, string? region = null)
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

            var capacitySpec = new DailyCapacitySpecification(engineer.Id, from);
            var dayAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(capacitySpec);
            var currentLoad = dayAssignments.Sum(a => a.WorkOrder.EstimatedHours);

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
        Team               = e.Team,
        Region             = e.Region,
        Skills             = e.Skills,
        WorkingHours       = e.WorkingHours,
        DailyCapacityHours = e.DailyCapacityHours,
        IsActive           = e.IsActive
    };
}
