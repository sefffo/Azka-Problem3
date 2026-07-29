using Azka.Services.DTOs.Dashboard;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IDashboardService
{
    Task<ApiResponse<DashboardDto>> GetDashboardAsync();

    /// <summary>
    /// Evicts the cached dashboard snapshot so the next request rebuilds
    /// from fresh data. Called by write operations in EngineerService,
    /// AssetService, and AssignmentService.
    /// </summary>
    void InvalidateDashboard();
}
