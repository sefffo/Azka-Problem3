using Azka.Services.DTOs.WorkOrder;
using FluentValidation;

namespace Azka.Services.Validators;

public class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrderDto>
{
    public CreateWorkOrderValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.EstimatedHours).GreaterThan(0).LessThanOrEqualTo(24);
        RuleFor(x => x.DueDate).GreaterThan(x => x.RequestedDate).WithMessage("Due date must be after requested date.");
    }
}
