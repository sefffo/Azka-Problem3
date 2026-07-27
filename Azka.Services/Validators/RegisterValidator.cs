using Azka.Services.DTOs.Auth;
using FluentValidation;

namespace Azka.Services.Validators;

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    // Valid roles that can be assigned at registration
    private static readonly string[] ValidRoles = ["Admin", "Dispatcher", "Engineer"];

    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Role)
            .Must(r => ValidRoles.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}.");
    }
}
