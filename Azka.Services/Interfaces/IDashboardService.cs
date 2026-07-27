using Azka.Services.DTOs.Dashboard;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IDashboardService
{
    Task<ApiResponse<DashboardDto>> GetDashboardAsync();
}
