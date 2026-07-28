using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assignments;
using Azka.Domain.Specifications.Engineers;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Engineer;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;

namespace Azka.Services.Implementation;

public class EngineerService(
    IUnitOfWork unitOfWork,
    AppDbContext context) : IEngineerService
{
    public async Task<ApiResponse<IEnumerable<EngineerDto>>> GetAllAsync()
    {
        var spec = new ActiveEngineersSpecification();
        var engineers = await unitOfWork.GetRepository<Engineer, int>().ListAsync(spec);
        return ApiResponse<IEnumerable<EngineerDto>>.Success(engineers.Select(MapToDto));
    }

    public async Task<ApiResponse<EngineerDto>> GetByIdAsync(int id)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);
        return ApiResponse<EngineerDto>.Success(MapToDto(engineer));
    }

    public async Task<ApiResponse<EngineerDto>> CreateAsync(CreateEngineerDto dto)
    {
        // Duplicate check via spec
        var duplicateSpec = new EngineerDuplicateSpecification(dto.EmployeeNumber);
        var count = await unitOfWork.GetRepository<Engineer, int>().CountAsync(duplicateSpec);
        if (count > 0)
            throw new ConflictException($"Engineer with employee number '{dto.EmployeeNumber}' already exists.");

        var engineer = new Engineer
        {
            EmployeeNumber = dto.EmployeeNumber,
            FullName = dto.FullName,
            Team = dto.Team,
            Region = dto.Region,
            Skills = dto.Skills,
            WorkingHours = dto.WorkingHours,
            DailyCapacityHours = dto.DailyCapacityHours,
            IsActive = true
        };

        var repo = unitOfWork.GetRepository<Engineer, int>();
        await repo.AddAsync(engineer);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<EngineerDto>.Success(MapToDto(engineer), "Engineer created successfully.");
    }

    public async Task<ApiResponse<EngineerDto>> UpdateAsync(int id, UpdateEngineerDto dto)
    {
        var engineer = await unitOfWork.GetRepository<Engineer, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Engineer), id);

        engineer.FullName = dto.FullName;
        engineer.Team = dto.Team;
        engineer.Region = dto.Region;
        engineer.Skills = dto.Skills;
        engineer.WorkingHours = dto.WorkingHours;
        engineer.DailyCapacityHours = dto.DailyCapacityHours;
        engineer.IsActive = dto.IsActive;

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

        var workloadSpec = new EngineerWorkloadSpecification(id, date);
        var activeAssignments = await unitOfWork.GetRepository<Assignment, int>().ListAsync(workloadSpec);

        var totalHours = activeAssignments.Sum(a => a.WorkOrder.EstimatedHours);
        var remaining = Math.Max(0, engineer.DailyCapacityHours - totalHours);
        var utilization = engineer.DailyCapacityHours > 0
            ? (totalHours / engineer.DailyCapacityHours) * 100
            : 0;

        return ApiResponse<EngineerWorkloadDto>.Success(new EngineerWorkloadDto
        {
            EngineerId = engineer.Id,
            FullName = engineer.FullName,
            Region = engineer.Region,
            AssignedWorkOrders = activeAssignments.Count,
            TotalEstimatedHours = totalHours,
            DailyCapacityHours = engineer.DailyCapacityHours,
            RemainingCapacityHours = remaining,
            UtilizationPercentage = Math.Round(utilization, 2)
        });
    }

    public async Task<ApiResponse<IEnumerable<EngineerDto>>> GetByRegionAsync(string region)
    {
        var spec = new EngineerByRegionSpecification(region);
        var engineers = await unitOfWork.GetRepository<Engineer, int>().ListAsync(spec);
        return ApiResponse<IEnumerable<EngineerDto>>.Success(engineers.Select(MapToDto));
    }

    private static EngineerDto MapToDto(Engineer e) => new()
    {
        Id = e.Id,
        EmployeeNumber = e.EmployeeNumber,
        FullName = e.FullName,
        Team = e.Team,
        Region = e.Region,
        Skills = e.Skills,
        WorkingHours = e.WorkingHours,
        DailyCapacityHours = e.DailyCapacityHours,
        IsActive = e.IsActive
    };
}
