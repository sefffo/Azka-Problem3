using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Specifications.Assignments;

namespace Azka.Tests.Specifications;

public class AssignmentConflictSpecificationTests
{
    private static readonly DateTime Base = new(2026, 7, 28);

    private static Assignment MakeAssignment(int id, int engineerId, DateTime start, DateTime end, AssignmentStatus status = AssignmentStatus.Assigned)
        => new()
        {
            Id = id,
            EngineerId = engineerId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = status
        };

    [Fact]
    public void NoConflict_WhenNoAssignmentsExist()
    {
        var assignments = new List<Assignment>();
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(9), Base.AddHours(11));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void Conflict_WhenOverlappingAssignmentExists()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10), Base.AddHours(12));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.True(result);
    }

    [Fact]
    public void NoConflict_WhenNonOverlappingAssignmentExists()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(11), Base.AddHours(13));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void NoConflict_WhenOverlappingButDifferentEngineer()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 2, Base.AddHours(9), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10), Base.AddHours(12));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void NoConflict_WhenOverlappingButCancelled()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(11), AssignmentStatus.Cancelled)
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10), Base.AddHours(12));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void NoConflict_WhenOverlappingButFailed()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(11), AssignmentStatus.Failed)
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10), Base.AddHours(12));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void NoConflict_WhenExcludingOwnId()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(5, 1, Base.AddHours(9), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10), Base.AddHours(12), excludeAssignmentId: 5);
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void Example_Ahmed0900To1100_Rejects1030To1200()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10.5), Base.AddHours(12));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.True(result);
    }

    [Fact]
    public void AdjacentSlots_NoConflict()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(11), Base.AddHours(13));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.False(result);
    }

    [Fact]
    public void NewSlotInsideExisting_Conflict()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(9), Base.AddHours(13))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(10), Base.AddHours(11));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.True(result);
    }

    [Fact]
    public void ExistingInsideNewSlot_Conflict()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, 1, Base.AddHours(10), Base.AddHours(11))
        };
        var spec = new AssignmentConflictSpecification(1, Base.AddHours(9), Base.AddHours(13));
        var result = assignments.AsQueryable().Any(spec.Criteria!.Compile());
        Assert.True(result);
    }
}