using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Azka.Services.Implementation.Email;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Azka.Tests.Services;

public class AssignmentServiceAutoAssignTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<WorkOrder, int>> _workOrderRepo = new();
    private readonly Mock<IGenericRepository<Engineer, int>> _engineerRepo = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly Mock<IGenericRepository<AssignmentHistory, int>> _historyRepo = new();
    private readonly Mock<IDbContextTransaction> _tx = new();
    private readonly IAssignmentService _service;

    private static readonly DateTime _start = new(2026, 7, 28, 9, 0, 0);
    private static readonly DateTime _end   = new(2026, 7, 28, 11, 0, 0);

    private static Engineer MakeEngineer(int id) => new()
    {
        Id = id, FullName = $"Engineer {id}", Email = "",
        WorkingHours = "08:00-16:00", DailyCapacityHours = 8, IsActive = true,
        EmployeeNumber = $"E{id}", Team = "T", Region = "R", Skills = "S"
    };

    private static WorkOrder MakeWorkOrder(WorkOrderStatus status = WorkOrderStatus.Open) => new()
    {
        Id = 1, WorkOrderNumber = "WO-001", Status = status, EstimatedHours = 2
    };

    public AssignmentServiceAutoAssignTests()
    {
        _uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(_workOrderRepo.Object);
        _uow.Setup(u => u.GetRepository<Engineer, int>()).Returns(_engineerRepo.Object);
        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(_historyRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_tx.Object);
        _service = new AssignmentService(_uow.Object, new BackgroundEmailQueue());
    }

    [Fact]
    public async Task AutoAssignAsync_WhenWorkOrderNotFound_ThrowsNotFoundException()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((WorkOrder?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 99, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenWorkOrderCancelled_ThrowsBadRequest()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeWorkOrder(WorkOrderStatus.Cancelled));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenWorkOrderCompleted_ThrowsBadRequest()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeWorkOrder(WorkOrderStatus.Completed));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenWorkOrderAlreadyAssigned_ThrowsBadRequest()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeWorkOrder(WorkOrderStatus.Assigned));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
        Assert.Contains("already assigned", ex.Message);
    }

    [Fact]
    public async Task AutoAssignAsync_WhenNoAvailableEngineer_ThrowsServiceUnavailable()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeWorkOrder());
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer>()); // nobody active

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenAllEngineersHaveConflicts_ThrowsServiceUnavailable()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeWorkOrder());
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { MakeEngineer(1) });
        // conflict exists for every engineer
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
    }

    [Fact]
    public async Task AutoAssignAsync_WhenCapacityExceededForAll_ThrowsServiceUnavailable()
    {
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeWorkOrder()); // EstimatedHours = 2
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { MakeEngineer(1) }); // DailyCapacityHours = 8
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);
        // Already has 7h booked; new 2h would push over 8h
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>
            {
                new() { WorkOrder = new WorkOrder { EstimatedHours = 7 } }
            });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            _service.AutoAssignAsync(new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin"));
    }

    [Fact]
    public async Task AutoAssignAsync_PicksEngineerWithLowestLoad()
    {
        var wo = MakeWorkOrder();
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(wo);

        var e1 = MakeEngineer(1); // will have 3h load
        var e2 = MakeEngineer(2); // will have 1h load → should be picked
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { e1, e2 });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false); // no conflicts

        // Return different loads per engineer via sequential setups
        _assignmentRepo.SetupSequence(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment> { new() { WorkOrder = new WorkOrder { EstimatedHours = 3 } } }) // e1
            .ReturnsAsync(new List<Assignment> { new() { WorkOrder = new WorkOrder { EstimatedHours = 1 } } }); // e2

        Assignment? saved = null;
        _assignmentRepo.Setup(r => r.AddAsync(It.IsAny<Assignment>()))
            .Callback<Assignment>(a => saved = a)
            .Returns(Task.CompletedTask);

        var result = await _service.AutoAssignAsync(
            new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin");

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.EngineerId); // e2 has lower load
        Assert.Equal(WorkOrderStatus.Assigned, wo.Status);
    }

    [Fact]
    public async Task AutoAssignAsync_Success_SetsAssignmentStatusToAssigned()
    {
        var wo = MakeWorkOrder();
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(wo);
        _engineerRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Engineer>>()))
            .ReturnsAsync(new List<Engineer> { MakeEngineer(1) });
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(new List<Assignment>());
        _assignmentRepo.Setup(r => r.AddAsync(It.IsAny<Assignment>())).Returns(Task.CompletedTask);

        var result = await _service.AutoAssignAsync(
            new AutoAssignDto { WorkOrderId = 1, ScheduledStart = _start, ScheduledEnd = _end }, "admin");

        Assert.True(result.Succeeded);
        Assert.Equal(AssignmentStatus.Assigned.ToString(), result.Data!.Status);
    }
}
