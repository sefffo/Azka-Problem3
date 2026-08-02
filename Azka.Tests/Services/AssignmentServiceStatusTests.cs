using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Azka.Services.Implementation.Email;
using Azka.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Azka.Tests.Services;

/// <summary>Tests for UpdateStatusAsync and CancelAsync on AssignmentService.</summary>
public class AssignmentServiceStatusTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly Mock<IGenericRepository<AssignmentHistory, int>> _historyRepo = new();
    private readonly Mock<IGenericRepository<WorkOrder, int>> _workOrderRepo = new();
    private readonly Mock<IDbContextTransaction> _tx = new();
    private readonly Mock<IDashboardService> _dashboardService = new();
    private readonly IAssignmentService _service;

    private static Assignment MakeAssignment(AssignmentStatus status = AssignmentStatus.Assigned) =>
        new()
        {
            Id             = 1,
            EngineerId     = 1,
            WorkOrderId    = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd   = new DateTime(2026, 7, 28, 11, 0, 0),
            Status         = status,
            Engineer       = new Engineer { Id = 1, FullName = "Ali", Email = "", WorkingHours = "08:00-16:00", IsActive = true },
            WorkOrder      = new WorkOrder { Id = 1, WorkOrderNumber = "WO-001", Status = WorkOrderStatus.Assigned, EstimatedHours = 2 }
        };

    public AssignmentServiceStatusTests()
    {
        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(_historyRepo.Object);
        _uow.Setup(u => u.GetRepository<WorkOrder, int>()).Returns(_workOrderRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_tx.Object);

        _service = new AssignmentService(_uow.Object, new BackgroundEmailQueue(), _dashboardService.Object);
    }

    // ── UpdateStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync((Assignment?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateStatusAsync(99, new UpdateAssignmentStatusDto { Status = AssignmentStatus.InProgress }));
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenAlreadyCompleted_ThrowsBadRequest()
    {
        var assignment = MakeAssignment(AssignmentStatus.Completed);
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.UpdateStatusAsync(1, new UpdateAssignmentStatusDto { Status = AssignmentStatus.InProgress }));
    }

    [Fact]
    public async Task UpdateStatusAsync_ToInProgress_UpdatesWorkOrderStatus()
    {
        var assignment = MakeAssignment(AssignmentStatus.Assigned);
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);

        var result = await _service.UpdateStatusAsync(1,
            new UpdateAssignmentStatusDto { Status = AssignmentStatus.InProgress });

        Assert.True(result.Succeeded);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status);
        Assert.Equal(WorkOrderStatus.InProgress, assignment.WorkOrder.Status);
        _historyRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentHistory>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToCompleted_UpdatesWorkOrderToCompleted()
    {
        var assignment = MakeAssignment(AssignmentStatus.InProgress);
        assignment.WorkOrder.Status = WorkOrderStatus.InProgress;
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);

        var result = await _service.UpdateStatusAsync(1,
            new UpdateAssignmentStatusDto { Status = AssignmentStatus.Completed });

        Assert.True(result.Succeeded);
        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
        Assert.Equal(WorkOrderStatus.Completed, assignment.WorkOrder.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_RecordsHistoryEntry()
    {
        var assignment = MakeAssignment(AssignmentStatus.Assigned);
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);
        AssignmentHistory? capturedHistory = null;
        _historyRepo.Setup(r => r.AddAsync(It.IsAny<AssignmentHistory>()))
            .Callback<AssignmentHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        await _service.UpdateStatusAsync(1,
            new UpdateAssignmentStatusDto { Status = AssignmentStatus.InProgress });

        Assert.NotNull(capturedHistory);
        Assert.Equal(1, capturedHistory!.AssignmentId);
        Assert.Equal("Assigned", capturedHistory.PreviousStatus);
    }

    // ── CancelAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync((Assignment?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelAsync(99, "admin"));
    }

    [Fact]
    public async Task CancelAsync_WhenCompleted_ThrowsBadRequest()
    {
        var assignment = MakeAssignment(AssignmentStatus.Completed);
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);

        await Assert.ThrowsAsync<BadRequestException>(() => _service.CancelAsync(1, "admin"));
    }

    [Fact]
    public async Task CancelAsync_WhenAssigned_SetsCancelledAndResetsWorkOrder()
    {
        var assignment = MakeAssignment(AssignmentStatus.Assigned);
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);

        var result = await _service.CancelAsync(1, "dispatcher");

        Assert.True(result.Succeeded);
        Assert.True(result.Data);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
        Assert.Equal(WorkOrderStatus.PendingAssignment, assignment.WorkOrder.Status);
        _historyRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentHistory>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_RecordsHistoryWithCancelledByUser()
    {
        var assignment = MakeAssignment(AssignmentStatus.Assigned);
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);
        AssignmentHistory? capturedHistory = null;
        _historyRepo.Setup(r => r.AddAsync(It.IsAny<AssignmentHistory>()))
            .Callback<AssignmentHistory>(h => capturedHistory = h)
            .Returns(Task.CompletedTask);

        await _service.CancelAsync(1, "dispatcher");

        Assert.NotNull(capturedHistory);
        Assert.Equal("dispatcher", capturedHistory!.ChangedBy);
        Assert.Equal("Cancelled", capturedHistory.ChangeReason);
    }

    [Fact]
    public async Task CancelAsync_WhenInProgress_AlsoCancels()
    {
        var assignment = MakeAssignment(AssignmentStatus.InProgress);
        assignment.WorkOrder.Status = WorkOrderStatus.InProgress;
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<ISpecification<Assignment>>()))
            .ReturnsAsync(assignment);

        var result = await _service.CancelAsync(1, "admin");

        Assert.True(result.Succeeded);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
    }
}
