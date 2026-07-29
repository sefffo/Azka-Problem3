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

public class AssignmentServiceRescheduleTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Assignment, int>> _assignmentRepo = new();
    private readonly Mock<IGenericRepository<AssignmentHistory, int>> _historyRepo = new();
    private readonly Mock<IDbContextTransaction> _tx = new();
    private readonly IAssignmentService _service;
    private readonly Assignment _existingAssignment;

    public AssignmentServiceRescheduleTests()
    {
        _existingAssignment = new Assignment
        {
            Id = 1,
            EngineerId = 1,
            WorkOrderId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0),
            Status = AssignmentStatus.Assigned,
            Engineer = new Engineer
            {
                Id = 1,
                FullName = "Ahmed",
                WorkingHours = "08:00-16:00",
                DailyCapacityHours = 8
            },
            WorkOrder = new WorkOrder { EstimatedHours = 2 }
        };

        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.Is<AssignmentByIdSpecification>(s => true)))
            .ReturnsAsync(_existingAssignment);

        _uow.Setup(u => u.GetRepository<Assignment, int>()).Returns(_assignmentRepo.Object);
        _uow.Setup(u => u.GetRepository<AssignmentHistory, int>()).Returns(_historyRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_tx.Object);

        _service = new AssignmentService(_uow.Object, new BackgroundEmailQueue());
    }

    [Fact]
    public async Task RescheduleAsync_WhenAssignmentNotFound_ThrowsNotFound()
    {
        _assignmentRepo.Setup(r => r.GetBySpecAsync(It.IsAny<AssignmentByIdSpecification>()))
            .ReturnsAsync((Assignment?)null);
        var dto = new RescheduleAssignmentDto { NewScheduledStart = DateTime.UtcNow, NewScheduledEnd = DateTime.UtcNow.AddHours(1), ChangeReason = "Test" };
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RescheduleAsync(999, dto, "Dispatcher"));
    }

    [Fact]
    public async Task RescheduleAsync_WhenCompleted_ThrowsBadRequest()
    {
        _existingAssignment.Status = AssignmentStatus.Completed;
        var dto = new RescheduleAssignmentDto { NewScheduledStart = DateTime.UtcNow, NewScheduledEnd = DateTime.UtcNow.AddHours(1), ChangeReason = "Test" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.RescheduleAsync(1, dto, "Dispatcher"));
    }

    [Fact]
    public async Task RescheduleAsync_WhenCancelled_ThrowsBadRequest()
    {
        _existingAssignment.Status = AssignmentStatus.Cancelled;
        var dto = new RescheduleAssignmentDto { NewScheduledStart = DateTime.UtcNow, NewScheduledEnd = DateTime.UtcNow.AddHours(1), ChangeReason = "Test" };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.RescheduleAsync(1, dto, "Dispatcher"));
    }

    [Fact]
    public async Task RescheduleAsync_WhenOutsideWorkingHours_ThrowsBadRequest()
    {
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = new DateTime(2026, 7, 28, 5, 0, 0),
            NewScheduledEnd = new DateTime(2026, 7, 28, 7, 0, 0),
            ChangeReason = "Client request"
        };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.RescheduleAsync(1, dto, "Dispatcher"));
    }

    [Fact]
    public async Task RescheduleAsync_WhenConflictExists_ThrowsConflict()
    {
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(true);
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = new DateTime(2026, 7, 28, 13, 0, 0),
            NewScheduledEnd = new DateTime(2026, 7, 28, 15, 0, 0),
            ChangeReason = "Client request"
        };
        await Assert.ThrowsAsync<ConflictException>(() => _service.RescheduleAsync(1, dto, "Dispatcher"));
    }

    [Fact]
    public async Task RescheduleAsync_WhenCapacityExceeded_ThrowsBadRequest()
    {
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(
            new List<Assignment>
            {
                new() { WorkOrder = new WorkOrder { EstimatedHours = 7 } }
            });

        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = new DateTime(2026, 7, 28, 13, 0, 0),
            NewScheduledEnd = new DateTime(2026, 7, 28, 15, 0, 0),
            ChangeReason = "Client request"
        };
        await Assert.ThrowsAsync<BadRequestException>(() => _service.RescheduleAsync(1, dto, "Dispatcher"));
    }

    [Fact]
    public async Task RescheduleAsync_Success_UpdatesAssignmentAndCreatesHistory()
    {
        _assignmentRepo.Setup(r => r.AnyAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(false);
        _assignmentRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Assignment>>())).ReturnsAsync(new List<Assignment>());
        AssignmentHistory? createdHistory = null;
        _historyRepo.Setup(r => r.AddAsync(It.IsAny<AssignmentHistory>()))
            .Callback<AssignmentHistory>(h => createdHistory = h)
            .Returns(Task.CompletedTask);

        var newStart = new DateTime(2026, 7, 28, 13, 0, 0);
        var newEnd = new DateTime(2026, 7, 28, 15, 0, 0);
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = newStart,
            NewScheduledEnd = newEnd,
            ChangeReason = "Client requested time change"
        };

        var result = await _service.RescheduleAsync(1, dto, "Dispatcher");

        Assert.True(result.Succeeded);
        Assert.Equal(newStart, _existingAssignment.ScheduledStart);
        Assert.Equal(newEnd, _existingAssignment.ScheduledEnd);
        Assert.NotNull(_existingAssignment.UpdatedAt);
        Assert.NotNull(createdHistory);
        Assert.Equal(1, createdHistory.AssignmentId);
        Assert.Equal(new DateTime(2026, 7, 28, 9, 0, 0), createdHistory.PreviousStart);
        Assert.Equal(new DateTime(2026, 7, 28, 11, 0, 0), createdHistory.PreviousEnd);
        Assert.Equal("Assigned", createdHistory.PreviousStatus);
        Assert.Equal("Dispatcher", createdHistory.ChangedBy);
        Assert.Equal("Client requested time change", createdHistory.ChangeReason);
        _assignmentRepo.Verify(r => r.Update(_existingAssignment), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
