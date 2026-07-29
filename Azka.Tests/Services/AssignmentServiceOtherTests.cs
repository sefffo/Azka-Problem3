using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Domain.Specifications.Assignments;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Azka.Services.Implementation.Email;
using Azka.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Azka.Tests.Services;

public class AssignmentServiceOtherTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly Mock<IGenericRepository<AssignmentHistory, int>> _historyRepo = new();
    private readonly Mock<IGenericRepository<WorkOrder, int>> _workOrderRepo = new();
    private readonly Mock<IDbContextTransaction> _tx = new();
    private readonly IAssignmentService _service;

    public AssignmentServiceOtherTests()
    {
        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(_historyRepo.Object);
        _uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(_workOrderRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_tx.Object);
        _service = new AssignmentService(_uow.Object, new BackgroundEmailQueue());
        _service = new AssignmentService(_uow.Object, new BackgroundEmailQueue());
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsAssignment()
    {
        var assignment = new Assignment
        {
            Id = 1,
            Engineer = new Engineer { FullName = "Ahmed" },
            WorkOrder = new WorkOrder { WorkOrderNumber = "WO-001" },
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0),
            Status = AssignmentStatus.Assigned,
            AssignedBy = "Dispatcher"
        };
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync(assignment);

        var result = await _service.GetByIdAsync(1);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Id);
        Assert.Equal("Ahmed", result.Data.EngineerName);
        Assert.Equal("WO-001", result.Data.WorkOrderNumber);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsNotFound()
    {
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync((Assignment?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
    }

    // ── UpdateStatusAsync ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_WhenCompletedAssignment_ThrowsBadRequest()
    {
        var assignment = new Assignment
        {
            Id = 1,
            Status = AssignmentStatus.Completed,
            WorkOrder = new WorkOrder()
        };
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync(assignment);

        var dto = new UpdateAssignmentStatusDto { Status = AssignmentStatus.InProgress };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateStatusAsync(1, dto));
    }

    [Fact]
    public async Task UpdateStatusAsync_ToInProgress_SyncsWorkOrder()
    {
        var workOrder = new WorkOrder { Status = WorkOrderStatus.Assigned };
        var assignment = new Assignment
        {
            Id = 1,
            Status = AssignmentStatus.Assigned,
            WorkOrder = workOrder,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        };
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync(assignment);
        AssignmentHistory? createdHistory = null;
        _historyRepo.Setup(r => r.AddAsync(It.IsAny<AssignmentHistory>()))
            .Callback<AssignmentHistory>(h => createdHistory = h)
            .Returns(Task.CompletedTask);

        var dto = new UpdateAssignmentStatusDto { Status = AssignmentStatus.InProgress };
        var result = await _service.UpdateStatusAsync(1, dto);

        Assert.True(result.Succeeded);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status);
        Assert.NotNull(createdHistory);
        Assert.Equal("Assigned", createdHistory.PreviousStatus);
        Assert.Equal("Status changed to InProgress", createdHistory.ChangeReason);
        _assignmentRepo.Verify(r => r.Update(assignment), Times.Once);
        _historyRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentHistory>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_SyncsWorkOrder()
    {
        var workOrder = new WorkOrder { Status = WorkOrderStatus.InProgress };
        var assignment = new Assignment
        {
            Id = 1,
            Status = AssignmentStatus.InProgress,
            WorkOrder = workOrder,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        };
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync(assignment);
        AssignmentHistory? createdHistory = null;
        _historyRepo.Setup(r => r.AddAsync(It.IsAny<AssignmentHistory>()))
            .Callback<AssignmentHistory>(h => createdHistory = h)
            .Returns(Task.CompletedTask);

        var dto = new UpdateAssignmentStatusDto { Status = AssignmentStatus.Completed };
        var result = await _service.UpdateStatusAsync(1, dto);

        Assert.True(result.Succeeded);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
        Assert.NotNull(createdHistory);
        Assert.Equal("InProgress", createdHistory.PreviousStatus);
        Assert.Equal("Status changed to Completed", createdHistory.ChangeReason);
        _historyRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentHistory>()), Times.Once);
    }

    // ── CancelAsync ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_WhenCompleted_ThrowsBadRequest()
    {
        var assignment = new Assignment { Id = 1, Status = AssignmentStatus.Completed, WorkOrder = new WorkOrder() };
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync(assignment);

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CancelAsync(1, "Dispatcher"));
    }

    [Fact]
    public async Task CancelAsync_Success_CreatesHistoryAndUpdatesWorkOrder()
    {
        var workOrder = new WorkOrder { Status = WorkOrderStatus.Assigned };
        var assignment = new Assignment
        {
            Id = 1,
            EngineerId = 1,
            Status = AssignmentStatus.Assigned,
            WorkOrder = workOrder,
            Engineer = new Engineer { FullName = "Ahmed", Email = "" },
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        };
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync(assignment);
        AssignmentHistory? createdHistory = null;
        _historyRepo.Setup(r => r.AddAsync(It.IsAny<AssignmentHistory>()))
            .Callback<AssignmentHistory>(h => createdHistory = h)
            .Returns(Task.CompletedTask);

        var result = await _service.CancelAsync(1, "Dispatcher");

        Assert.True(result.Succeeded);
        Assert.True(result.Data);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
        Assert.Equal(WorkOrderStatus.PendingAssignment, workOrder.Status);
        Assert.NotNull(createdHistory);
        Assert.Equal("Cancelled", createdHistory.ChangeReason);
        Assert.Equal("Dispatcher", createdHistory.ChangedBy);
        _assignmentRepo.Verify(r => r.Update(assignment), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResults()
    {
        var assignments = new List<Assignment>
        {
            new() { Id = 1, Engineer = new Engineer { FullName = "E1" }, WorkOrder = new WorkOrder { WorkOrderNumber = "WO-1" }, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1), Status = AssignmentStatus.Assigned },
            new() { Id = 2, Engineer = new Engineer { FullName = "E2" }, WorkOrder = new WorkOrder { WorkOrderNumber = "WO-2" }, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(2), Status = AssignmentStatus.Assigned }
        };
        _assignmentRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(2);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(assignments);

        var result = await _service.GetAllAsync(new AssignmentQueryDto());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count());
    }
}
