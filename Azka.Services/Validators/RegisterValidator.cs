using Azka.Services.DTOs.Auth;
using FluentValidation;

namespace Azka.Services.Validators;

public class RegisterValidator : AbstractValidator<RegisterDto>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Role).Must(r => r == "Admin" || r == "Dispatcher").WithMessage("Role must be Admin or Dispatcher.");
    }
}
