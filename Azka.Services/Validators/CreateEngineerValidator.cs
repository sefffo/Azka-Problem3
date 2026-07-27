using Azka.Services.DTOs.Engineer;
using FluentValidation;

namespace Azka.Services.Validators;

public class CreateEngineerValidator : AbstractValidator<CreateEngineerDto>
{
    public CreateEngineerValidator()
    {
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Team).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DailyCapacityHours).GreaterThan(0).LessThanOrEqualTo(24);
    }
}
