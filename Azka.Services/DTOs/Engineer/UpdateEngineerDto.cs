using FluentValidation;

namespace Azka.Services.DTOs.Engineer;

public class UpdateEngineerDto
{
    public string FullName           { get; set; } = string.Empty;
    public string Email              { get; set; } = string.Empty;
    public string Team               { get; set; } = string.Empty;
    public string Region             { get; set; } = string.Empty;
    public string Skills             { get; set; } = string.Empty;
    public string WorkingHours       { get; set; } = "08:00-16:00";
    public double DailyCapacityHours { get; set; } = 8.0;
    public bool   IsActive           { get; set; } = true;
}

public class UpdateEngineerDtoValidator : AbstractValidator<UpdateEngineerDto>
{
    public UpdateEngineerDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Team).NotEmpty();
        RuleFor(x => x.Region).NotEmpty();
        RuleFor(x => x.WorkingHours).NotEmpty();
        RuleFor(x => x.DailyCapacityHours).GreaterThan(0);
    }
}
