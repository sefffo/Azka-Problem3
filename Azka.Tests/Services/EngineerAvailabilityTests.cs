using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Domain.Specifications.Engineers;
using Azka.Domain.Specifications.Assignments;
using Azka.Services.DTOs.Assignment;
using Azka.Services.DTOs.Engineer;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Azka.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Azka.Tests.Services;

public class EngineerAvailabilityTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Engineer, int>> _engineerRepo = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly Mock<IDbContextTransaction> _tx = new();
    private readonly IAssignmentService _assignmentService;
    private readonly IEngineerService _engineerService;
    private readonly Engineer _engineer;

    public EngineerAvailabilityTests()
    {
        _engineer = new Engineer
        {
            Id = 1,
            FullName = "Ahmed",
            IsActive = true,
            WorkingHours = "08:00-16:00",
            DailyCapacityHours = 8,
            Team = "Alpha",
            Region = "Cairo",
            Skills = "Electrical"
        };

        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(_engineer);
        _engineerRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Engineer?)null);

        _uow.Setup(u => u.GetRepository<Engineer, int>()).Returns(_engineerRepo.Object);
        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(new Mock<IGenericRepository<AssignmentHistory, int>>().Object);
        _uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(new Mock<IGenericRepository<WorkOrder, int>>().Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_tx.Object);

        _assignmentService = new AssignmentService(_uow.Object);
        _engineerService = new EngineerService(_uow.Object);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenNoEngineers_ReturnsEmpty()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer>());

        var result = await _engineerService.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenAvailable_ReturnsEngineer()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { _engineer });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>());

        var result = await _engineerService.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal("Ahmed", result.Data![0].FullName);
        Assert.Equal(8, result.Data[0].RemainingCapacityHours);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenOutsideWorkingHours_Excludes()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { _engineer });

        var result = await _engineerService.GetAvailableAsync(
            new DateTime(2026, 7, 28, 6, 0, 0),
            new DateTime(2026, 7, 28, 8, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenConflictExists_Excludes()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { _engineer });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(true);

        var result = await _engineerService.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_WhenCapacityExceeded_Excludes()
    {
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { _engineer });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>
            {
                new() {
                    WorkOrder = new WorkOrder { EstimatedHours = 7 },
                    ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
                    ScheduledEnd = new DateTime(2026, 7, 28, 16, 0, 0)
                }
            });

        var result = await _engineerService.GetAvailableAsync(
            new DateTime(2026, 7, 28, 9, 0, 0),
            new DateTime(2026, 7, 28, 11, 0, 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetAvailableAsync_EndBeforeStart_ReturnsFailure()
    {
        var result = await _engineerService.GetAvailableAsync(
            new DateTime(2026, 7, 28, 11, 0, 0),
            new DateTime(2026, 7, 28, 9, 0, 0));

        Assert.False(result.Succeeded);
    }

    // ── AutoAssignAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task AutoAssignAsync_WhenWorkOrderNotFound_ThrowsNotFound()
    {
        var woRepoMock = new Mock<IGenericRepository<WorkOrder, int>>();
        woRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((WorkOrder?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(woRepoMock.Object);
        var svc = new AssignmentService(uow.Object);

        var dto = new AutoAssignDto { WorkOrderId = 99, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<NotFoundException>(() => svc.AutoAssignAsync(dto, "System"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenWorkOrderCancelled_ThrowsBadRequest()
    {
        var wo = new WorkOrder { Id = 1, Status = WorkOrderStatus.Cancelled };
        var woRepoMock = new Mock<IGenericRepository<WorkOrder, int>>();
        woRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(wo);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(woRepoMock.Object);
        var svc = new AssignmentService(uow.Object);

        var dto = new AutoAssignDto { WorkOrderId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<BadRequestException>(() => svc.AutoAssignAsync(dto, "System"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenNoAvailableEngineer_ThrowsServiceUnavailable()
    {
        var wo = new WorkOrder { Id = 1, Status = WorkOrderStatus.Open, EstimatedHours = 2 };
        var woRepoMock = new Mock<IGenericRepository<WorkOrder, int>>();
        woRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(wo);

        var engineerRepoMock = new Mock<IGenericRepository<Engineer, int>>();
        engineerRepoMock.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer>());

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(woRepoMock.Object);
        uow.Setup(u => u.GetRepository<Engineer, int>()).Returns(engineerRepoMock.Object);
        var svc = new AssignmentService(uow.Object);

        var dto = new AutoAssignDto { WorkOrderId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => svc.AutoAssignAsync(dto, "System"));
    }

    [Fact]
    public async Task AutoAssignAsync_Success_AssignsToLeastLoadedEngineer()
    {
        var wo = new WorkOrder { Id = 1, Status = WorkOrderStatus.Open, EstimatedHours = 2, WorkOrderNumber = "WO-001" };

        var busyEng = new Engineer { Id = 2, FullName = "Busy", WorkingHours = "08:00-16:00", DailyCapacityHours = 8 };
        var freeEng = new Engineer { Id = 3, FullName = "Free", WorkingHours = "08:00-16:00", DailyCapacityHours = 8 };

        var woRepoMock = new Mock<IGenericRepository<WorkOrder, int>>();
        woRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(wo);

        var engineerRepoMock = new Mock<IGenericRepository<Engineer, int>>();
        engineerRepoMock.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { busyEng, freeEng });

        var assignmentRepoMock = new Mock<IGenericRepository<Assignment, int>>();
        assignmentRepoMock.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);

        // busyEng has 5h load, freeEng has 0h load
        assignmentRepoMock.SetupSequence(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment> { new() { WorkOrder = new WorkOrder { EstimatedHours = 5 } } })
            .ReturnsAsync(new List<Assignment>());

        var historyRepoMock = new Mock<IGenericRepository<AssignmentHistory, int>>();
        var txMock = new Mock<IDbContextTransaction>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(woRepoMock.Object);
        uow.Setup(u => u.GetRepository<Engineer, int>()).Returns(engineerRepoMock.Object);
        uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(assignmentRepoMock.Object);
        uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(historyRepoMock.Object);
        uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(txMock.Object);

        var svc = new AssignmentService(uow.Object);
        Assignment? captured = null;
        assignmentRepoMock.Setup(r => r.AddAsync(It.IsAny<Assignment>()))
            .Callback<Assignment>(a => captured = a)
            .Returns(Task.CompletedTask);

        var result = await svc.AutoAssignAsync(new AutoAssignDto
        {
            WorkOrderId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        }, "System");

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal(3, captured.EngineerId); // freeEng (id=3) has lowest load
        Assert.Equal(AssignmentStatus.Assigned, captured.Status);
        Assert.Equal(WorkOrderStatus.Assigned, wo.Status);
    }
}
