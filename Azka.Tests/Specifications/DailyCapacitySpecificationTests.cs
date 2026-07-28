using Azka.Domain.Entities;
using Azka.Domain.Enums;
using Azka.Domain.Specifications.Assignments;

namespace Azka.Tests.Specifications;

public class DailyCapacitySpecificationTests
{
    private static readonly DateTime Base = new(2026, 7, 28);

    private static Assignment MakeAssignment(int engineerId, DateTime start, DateTime end, double estimatedHours, AssignmentStatus status = AssignmentStatus.Assigned)
        => new()
        {
            EngineerId = engineerId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = status,
            WorkOrder = new WorkOrder { EstimatedHours = estimatedHours }
        };

    [Fact]
    public void IncludesAssignment_OnSameDay()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, Base.AddHours(9), Base.AddHours(11), 2)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void ExcludesAssignment_OnDifferentDay()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, Base.AddDays(1).AddHours(9), Base.AddDays(1).AddHours(11), 2)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void ExcludesCancelledAssignment()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, Base.AddHours(9), Base.AddHours(11), 2, AssignmentStatus.Cancelled)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void ExcludesFailedAssignment()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, Base.AddHours(9), Base.AddHours(11), 2, AssignmentStatus.Failed)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void IncludesAssignment_SpansAcrossMidnight()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, Base.AddDays(-1).AddHours(22), Base.AddHours(2), 4)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void IncludesAssignment_EndsAtMidnight()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(1, Base.AddHours(22), Base.AddDays(1), 2)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void FiltersByEngineerId()
    {
        var assignments = new List<Assignment>
        {
            MakeAssignment(2, Base.AddHours(9), Base.AddHours(11), 2)
        };
        var spec = new DailyCapacitySpecification(1, Base);
        var result = assignments.AsQueryable().Where(spec.Criteria!.Compile()).ToList();
        Assert.Empty(result);
    }
}