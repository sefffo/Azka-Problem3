using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Azka.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Azka.Tests.Services;

public class AssignmentServiceCreateTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Engineer, int>> _engineerRepo = new();
    private readonly Mock<IGenericRepository<WorkOrder, int>> _workOrderRepo = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly Mock<IGenericRepository<AssignmentHistory, int>> _historyRepo = new();
    private readonly Mock<IDbContextTransaction> _tx = new();
    private readonly IAssignmentService _service;
    private readonly Engineer _engineer;
    private readonly WorkOrder _workOrder;

    public AssignmentServiceCreateTests()
    {
        _engineer = new Engineer
        {
            Id = 1,
            FullName = "Ahmed",
            IsActive = true,
            WorkingHours = "08:00-16:00",
            DailyCapacityHours = 8
        };

        _workOrder = new WorkOrder
        {
            Id = 1,
            WorkOrderNumber = "WO-001",
            Status = WorkOrderStatus.Open,
            EstimatedHours = 2
        };

        _engineerRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(_engineer);
        _engineerRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Engineer?)null);
        _workOrderRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(_workOrder);
        _workOrderRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((WorkOrder?)null);

        _uow.Setup(u => u.GetRepository<Engineer, int>()).Returns(_engineerRepo.Object);
        _uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(_workOrderRepo.Object);
        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(_historyRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_tx.Object);

        _service = new AssignmentService(_uow.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenEngineerNotFound_ThrowsNotFound()
    {
        var dto = new CreateAssignmentDto { EngineerId = 99, WorkOrderId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenEngineerInactive_ThrowsBadRequest()
    {
        _engineer.IsActive = false;
        var dto = new CreateAssignmentDto { EngineerId = 1, WorkOrderId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenWorkOrderNotFound_ThrowsNotFound()
    {
        var dto = new CreateAssignmentDto
        {
            EngineerId = 1,
            WorkOrderId = 99,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        };
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenWorkOrderCancelled_ThrowsBadRequest()
    {
        _workOrder.Status = WorkOrderStatus.Cancelled;
        var dto = new CreateAssignmentDto { EngineerId = 1, WorkOrderId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenWorkOrderCompleted_ThrowsBadRequest()
    {
        _workOrder.Status = WorkOrderStatus.Completed;
        var dto = new CreateAssignmentDto { EngineerId = 1, WorkOrderId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenOutsideWorkingHours_ThrowsBadRequest()
    {
        var dto = new CreateAssignmentDto
        {
            EngineerId = 1,
            WorkOrderId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 6, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 7, 0, 0)
        };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenConflictExists_ThrowsConflict()
    {
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(true);
        var dto = new CreateAssignmentDto
        {
            EngineerId = 1,
            WorkOrderId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 10, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 12, 0, 0)
        };
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_WhenCapacityExceeded_ThrowsBadRequest()
    {
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(false);
        var existingAssignments = new List<Assignment>
        {
            new()
            {
                WorkOrder = new WorkOrder { EstimatedHours = 7 },
                ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
                ScheduledEnd = new DateTime(2026, 7, 28, 16, 0, 0)
            }
        };
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(existingAssignments);
        var dto = new CreateAssignmentDto
        {
            EngineerId = 1,
            WorkOrderId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 10, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 12, 0, 0)
        };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto, "Dispatcher"));
    }

    [Fact]
    public async Task CreateAsync_Success_CreatesAssignmentAndUpdatesWorkOrder()
    {
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(new List<Assignment>());
        Assignment? createdAssignment = null;
        _assignmentRepo.Setup(r => r.AddAsync(It.IsAny<Assignment>()))
            .Callback<Assignment>(a => createdAssignment = a)
            .Returns(Task.CompletedTask);

        var dto = new CreateAssignmentDto
        {
            EngineerId = 1,
            WorkOrderId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        };

        var result = await _service.CreateAsync(dto, "Dispatcher");

        Assert.True(result.Succeeded);
        Assert.NotNull(createdAssignment);
        Assert.Equal(AssignmentStatus.Assigned, createdAssignment.Status);
        Assert.Equal("Dispatcher", createdAssignment.AssignedBy);
        Assert.Equal(WorkOrderStatus.Assigned, _workOrder.Status);
        _workOrderRepo.Verify(r => r.Update(_workOrder), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}