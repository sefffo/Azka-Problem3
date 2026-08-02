using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Engineer;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Azka.Tests.Services;

public class EngineerServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Engineer, int>> _engineerRepo = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly EngineerService _service;

    private static Engineer MakeEngineer(int id = 1) => new()
    {
        Id = id, EmployeeNumber = $"EMP-{id:D3}", FullName = $"Engineer {id}",
        Email = $"eng{id}@test.com", Team = "Alpha", Region = "Cairo",
        Skills = "Electrical", WorkingHours = "08:00-16:00",
        DailyCapacityHours = 8, IsActive = true
    };

    public EngineerServiceTests()
    {
        _uow.Setup(u => u.GetRepository<Engineer, int>()).Returns(_engineerRepo.Object);
        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Default: no assignments overlap today, so booked hours are 0.
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>());

        // IMemoryCache mock: TryGetValue always misses so every call hits the DB.
        var cache = new Mock<IMemoryCache>();
        var cacheEntry = new Mock<ICacheEntry>();
        cache.Setup(c => c.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
             .Returns(false);
        cache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);

        // DashboardService needs IUnitOfWork, AppDbContext, IMemoryCache.
        // We only need it so InvalidateDashboard() can be called safely —
        // use a null AppDbContext reference; it is never accessed in write paths.
        var dashboardService = new DashboardService(_uow.Object, null!, cache.Object);

        _service = new EngineerService(_uow.Object, cache.Object, dashboardService);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResult()
    {
        var engineers = new List<Engineer> { MakeEngineer(1), MakeEngineer(2) };
        _engineerRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(2);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(engineers);

        var result = await _service.GetAllAsync(new EngineerQueryDto { Page = 1, PageSize = 10 });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
    }

    [Fact]
    public async Task GetAllAsync_EmptyList_ReturnsZeroTotal()
    {
        _engineerRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(0);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(new List<Engineer>());

        var result = await _service.GetAllAsync(new EngineerQueryDto { Page = 1, PageSize = 10 });

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_ComputesBookedHoursToday()
    {
        var engineers = new List<Engineer> { MakeEngineer(1) };
        _engineerRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(1);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(engineers);

        var today = DateTime.Today;
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>
            {
                new()
                {
                    Id = 1,
                    EngineerId = 1,
                    WorkOrder = new WorkOrder { EstimatedHours = 3 },
                    ScheduledStart = today.AddHours(9),
                    ScheduledEnd = today.AddHours(12),
                    Status = AssignmentStatus.Assigned
                }
            });

        var result = await _service.GetAllAsync(new EngineerQueryDto { Page = 1, PageSize = 10 });

        var dto = result.Data!.Items.Single();
        Assert.Equal(3, dto.BookedHoursToday);
        Assert.Equal(37.5, dto.UtilizationPercentage);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsDto()
    {
        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeEngineer(1));

        var result = await _service.GetByIdAsync(1);

        Assert.True(result.Succeeded);
        Assert.Equal("EMP-001", result.Data!.EmployeeNumber);
        Assert.Equal("Cairo", result.Data.Region);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _engineerRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Engineer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(99));
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenDuplicateEmployeeNumber_ThrowsConflict()
    {
        _engineerRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(1);

        var dto = new CreateEngineerDto { EmployeeNumber = "EMP-001", FullName = "Duplicate",
            Email = "d@test.com", Team = "A", Region = "Cairo", Skills = "X",
            WorkingHours = "08:00-16:00", DailyCapacityHours = 8 };

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_WhenUnique_AddsEngineerAndSaves()
    {
        _engineerRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(0);
        Engineer? saved = null;
        _engineerRepo.Setup(r => r.AddAsync(It.IsAny<Engineer>()))
            .Callback<Engineer>(e => saved = e)
            .Returns(Task.CompletedTask);

        var dto = new CreateEngineerDto { EmployeeNumber = "EMP-010", FullName = "New Guy",
            Email = "new@test.com", Team = "Beta", Region = "Alex", Skills = "Mech",
            WorkingHours = "09:00-17:00", DailyCapacityHours = 7 };

        var result = await _service.CreateAsync(dto);

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.Equal("EMP-010", saved!.EmployeeNumber);
        Assert.True(saved.IsActive);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Success_DefaultsIsActiveToTrue()
    {
        _engineerRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Engineer>>())).ReturnsAsync(0);
        Engineer? saved = null;
        _engineerRepo.Setup(r => r.AddAsync(It.IsAny<Engineer>()))
            .Callback<Engineer>(e => saved = e)
            .Returns(Task.CompletedTask);

        var dto = new CreateEngineerDto { EmployeeNumber = "EMP-011", FullName = "Test",
            Email = "t@t.com", Team = "A", Region = "R", Skills = "S",
            WorkingHours = "08:00-16:00", DailyCapacityHours = 8 };

        await _service.CreateAsync(dto);

        Assert.True(saved!.IsActive);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _engineerRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Engineer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(99, new UpdateEngineerDto
            {
                FullName = "X", Email = "x@x.com", Team = "A", Region = "R",
                Skills = "S", WorkingHours = "08:00-16:00", DailyCapacityHours = 8, IsActive = true
            }));
    }

    [Fact]
    public async Task UpdateAsync_WhenFound_UpdatesAllFields()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(engineer);

        var dto = new UpdateEngineerDto { FullName = "Updated Name", Email = "upd@test.com",
            Team = "Gamma", Region = "Luxor", Skills = "Civil",
            WorkingHours = "07:00-15:00", DailyCapacityHours = 7, IsActive = false };

        var result = await _service.UpdateAsync(1, dto);

        Assert.True(result.Succeeded);
        Assert.Equal("Updated Name", engineer.FullName);
        Assert.Equal("Luxor", engineer.Region);
        Assert.False(engineer.IsActive);
        _engineerRepo.Verify(r => r.Update(engineer), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ── DeleteAsync (soft-delete) ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _engineerRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Engineer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(99));
    }

    [Fact]
    public async Task DeleteAsync_WhenFound_DeactivatesEngineer()
    {
        var engineer = MakeEngineer(2);
        _engineerRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(engineer);

        var result = await _service.DeleteAsync(2);

        Assert.True(result.Succeeded);
        Assert.True(result.Data);
        Assert.False(engineer.IsActive);
        _engineerRepo.Verify(r => r.Update(engineer), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ── GetWorkloadAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkloadAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _engineerRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Engineer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetWorkloadAsync(99, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetWorkloadAsync_WithNoAssignments_ReturnsFullCapacity()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(engineer);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>());

        var result = await _service.GetWorkloadAsync(1, DateTime.UtcNow);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Data!.TotalEstimatedHours);
        Assert.Equal(8, result.Data.RemainingCapacityHours);
        Assert.Equal(0, result.Data.UtilizationPercentage);
    }

    [Fact]
    public async Task GetWorkloadAsync_WithAssignments_CalculatesCorrectly()
    {
        var engineer = MakeEngineer(1); // DailyCapacityHours = 8
        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(engineer);

        var assignments = new List<Assignment>
        {
            new() { WorkOrder = new WorkOrder { EstimatedHours = 3 } },
            new() { WorkOrder = new WorkOrder { EstimatedHours = 2 } }
        };
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignments);

        var result = await _service.GetWorkloadAsync(1, DateTime.UtcNow);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.Data!.TotalEstimatedHours);
        Assert.Equal(3, result.Data.RemainingCapacityHours);
        Assert.Equal(62.5, result.Data.UtilizationPercentage);
    }

    [Fact]
    public async Task GetWorkloadAsync_WhenFullyBooked_ReturnsZeroRemaining()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(engineer);

        var assignments = new List<Assignment>
        {
            new() { WorkOrder = new WorkOrder { EstimatedHours = 8 } }
        };
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignments);

        var result = await _service.GetWorkloadAsync(1, DateTime.UtcNow);

        Assert.Equal(0, result.Data!.RemainingCapacityHours);
        Assert.Equal(100, result.Data.UtilizationPercentage);
    }

    // ── GetAvailableAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableAsync_WhenEndBeforeStart_ReturnsFailed()
    {
        var from = new DateTime(2026, 7, 28, 12, 0, 0);
        var to   = new DateTime(2026, 7, 28, 10, 0, 0);

        var result = await _service.GetAvailableAsync(from, to);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenNoEngineers_ReturnsEmpty()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer>());

        var result = await _service.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenEngineerOutsideWorkingHours_IsExcluded()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { engineer });

        var result = await _service.GetAvailableAsync(
            new DateTime(2026, 7, 28, 6, 0, 0),
            new DateTime(2026, 7, 28, 8, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenEngineerHasConflict_IsExcluded()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { engineer });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(true);

        var result = await _service.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenEngineerCapacityExceeded_IsExcluded()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { engineer });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>
            {
                new() { WorkOrder = new WorkOrder { EstimatedHours = 7 },
                        ScheduledStart = new DateTime(2026, 7, 28, 8, 0, 0),
                        ScheduledEnd   = new DateTime(2026, 7, 28, 15, 0, 0) }
            });

        var result = await _service.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenEngineerFits_IsIncluded()
    {
        var engineer = MakeEngineer(1);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { engineer });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>());

        var result = await _service.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal(1, result.Data!.First().EngineerId);
    }

    [Fact]
    public async Task GetAvailableAsync_WithRegionFilter_QueriesWithRegion()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer>());

        var result = await _service.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0),
            region: "Cairo");

        Assert.True(result.Succeeded);
        _engineerRepo.Verify(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()), Times.Once);
    }
}
