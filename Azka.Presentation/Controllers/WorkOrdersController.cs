using Azka.Services.DTOs.Search;
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
    /// <summary>Get all work orders</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await workOrderService.GetAllAsync());

    /// <summary>Get work order by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await workOrderService.GetByIdAsync(id));

    /// <summary>Search and filter work orders with pagination</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] WorkOrderSearchDto searchDto)
        => Ok(await workOrderService.SearchAsync(searchDto));

    /// <summary>Create a new work order</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkOrderDto dto)
    {
        var result = await workOrderService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>Update work order status</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateWorkOrderStatusDto dto)
        => Ok(await workOrderService.UpdateStatusAsync(id, dto));

    /// <summary>Cancel a work order</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id)
        => Ok(await workOrderService.CancelAsync(id));
}
