using Azka.Services.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Azka.Services.DI;

public static class ServicesRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateEngineerValidator>();
        return services;
    }
}
