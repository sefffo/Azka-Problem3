using Azka.Services.DTOs.Assignment;
using FluentValidation;

namespace Azka.Services.Validators;

public class RescheduleAssignmentValidator : AbstractValidator<RescheduleAssignmentDto>
{
    public RescheduleAssignmentValidator()
    {
        RuleFor(x => x.NewScheduledEnd)
            .GreaterThan(x => x.NewScheduledStart)
            .WithMessage("New scheduled end must be after new scheduled start.");
        RuleFor(x => x.ChangeReason)
            .NotEmpty().WithMessage("Change reason is required.")
            .MaximumLength(500).WithMessage("Change reason must not exceed 500 characters.");
    }
}