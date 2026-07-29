using Azka.Services.Implementation;
using Azka.Services.Implementation.Email;
using Azka.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Azka.Services.Implementation.DI;

public static class ImplementationRegistration
{
    public static IServiceCollection AddServiceImplementations(this IServiceCollection services)
    {
        // ── Memory Cache ─────────────────────────────────────────────────────
        // SizeLimit enables memory pressure eviction. Each cached entry costs 1 unit.
        services.AddMemoryCache(o => o.SizeLimit = 256);

        // ── Domain services ──────────────────────────────────────────────────
        // DashboardService is registered as Scoped AND exposed concretely so
        // EngineerService / AssetService can call InvalidateDashboard() on it.
        services.AddScoped<DashboardService>();
        services.AddScoped<IDashboardService>(sp => sp.GetRequiredService<DashboardService>());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEngineerService, EngineerService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IAssignmentService, AssignmentService>();

        // ── Email (background queue + worker + SMTP sender) ──────────────────
        services.AddSingleton<BackgroundEmailQueue>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHostedService<EmailSenderBackgroundService>();

        return services;
    }
}
