using Azka.Services.DTOs.WorkOrder;
using Azka.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Azka.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkOrdersController(IWorkOrderService workOrderService) : ControllerBase
{
    /// <summary>
    /// Get work orders. All query params are optional.
    /// Omit everything → paginated list of all work orders.
    /// Pass any combination of filters → scoped result.
    /// Replaces the old GET /api/WorkOrders + GET /api/WorkOrders/search endpoints.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] WorkOrderQueryDto query)
        => Ok(await workOrderService.GetAllAsync(query));

    /// <summary>Get a single work order by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await workOrderService.GetByIdAsync(id));

    /// <summary>Create a new work order (Admin or Dispatcher only)</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> Create([FromBody] CreateWorkOrderDto dto)
    {
        var result = await workOrderService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>Update work order status</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateWorkOrderStatusDto dto)
        => Ok(await workOrderService.UpdateStatusAsync(id, dto));

    /// <summary>Cancel a work order (Admin or Dispatcher only)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> Cancel(int id)
        => Ok(await workOrderService.CancelAsync(id));
}
