using Azka.Services.Implementation;
using Azka.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Azka.Services.Implementation.DI;

public static class ImplementationRegistration
{
    public static IServiceCollection AddServiceImplementations(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEngineerService, EngineerService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
