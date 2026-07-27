using Azka.Services.DTOs.Assignment;
using FluentValidation;

namespace Azka.Services.Validators;

public class CreateAssignmentValidator : AbstractValidator<CreateAssignmentDto>
{
    public CreateAssignmentValidator()
    {
        RuleFor(x => x.WorkOrderId).GreaterThan(0);
        RuleFor(x => x.EngineerId).GreaterThan(0);
        RuleFor(x => x.ScheduledEnd).GreaterThan(x => x.ScheduledStart).WithMessage("Scheduled end must be after scheduled start.");
    }
}
