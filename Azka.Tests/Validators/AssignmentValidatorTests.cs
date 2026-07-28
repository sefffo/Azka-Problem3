using Azka.Services.DTOs.Assignment;
using Azka.Services.Validators;
using FluentValidation.TestHelper;

namespace Azka.Tests.Validators;

public class CreateAssignmentValidatorTests
{
    private readonly CreateAssignmentValidator _validator = new();

    [Fact]
    public void CreateAssignment_ValidDto_PassesValidation()
    {
        var dto = new CreateAssignmentDto
        {
            WorkOrderId = 1,
            EngineerId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 11, 0, 0)
        };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateAssignment_WorkOrderIdZero_Fails()
    {
        var dto = new CreateAssignmentDto { WorkOrderId = 0, EngineerId = 1, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.WorkOrderId);
    }

    [Fact]
    public void CreateAssignment_EngineerIdZero_Fails()
    {
        var dto = new CreateAssignmentDto { WorkOrderId = 1, EngineerId = 0, ScheduledStart = DateTime.UtcNow, ScheduledEnd = DateTime.UtcNow.AddHours(1) };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.EngineerId);
    }

    [Fact]
    public void CreateAssignment_EndBeforeStart_Fails()
    {
        var dto = new CreateAssignmentDto
        {
            WorkOrderId = 1,
            EngineerId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 11, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 9, 0, 0)
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ScheduledEnd);
    }

    [Fact]
    public void CreateAssignment_EndEqualToStart_Fails()
    {
        var dto = new CreateAssignmentDto
        {
            WorkOrderId = 1,
            EngineerId = 1,
            ScheduledStart = new DateTime(2026, 7, 28, 9, 0, 0),
            ScheduledEnd = new DateTime(2026, 7, 28, 9, 0, 0)
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ScheduledEnd);
    }
}

public class RescheduleAssignmentValidatorTests
{
    private readonly RescheduleAssignmentValidator _validator = new();

    [Fact]
    public void RescheduleAssignment_ValidDto_PassesValidation()
    {
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = new DateTime(2026, 7, 28, 13, 0, 0),
            NewScheduledEnd = new DateTime(2026, 7, 28, 15, 0, 0),
            ChangeReason = "Client request"
        };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RescheduleAssignment_EndBeforeStart_Fails()
    {
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = new DateTime(2026, 7, 28, 15, 0, 0),
            NewScheduledEnd = new DateTime(2026, 7, 28, 13, 0, 0),
            ChangeReason = "Test"
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.NewScheduledEnd);
    }

    [Fact]
    public void RescheduleAssignment_EmptyChangeReason_Fails()
    {
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = DateTime.UtcNow,
            NewScheduledEnd = DateTime.UtcNow.AddHours(1),
            ChangeReason = ""
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ChangeReason);
    }

    [Fact]
    public void RescheduleAssignment_LongChangeReason_Fails()
    {
        var dto = new RescheduleAssignmentDto
        {
            NewScheduledStart = DateTime.UtcNow,
            NewScheduledEnd = DateTime.UtcNow.AddHours(1),
            ChangeReason = new string('x', 501)
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ChangeReason);
    }
}