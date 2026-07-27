using Azka.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Azka.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Get operational dashboard with engineer and work order summary</summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
        => Ok(await dashboardService.GetDashboardAsync());
}
