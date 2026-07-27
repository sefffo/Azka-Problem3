using System.Security.Claims;
using Azka.Services.DTOs.Assignment;
using Azka.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Azka.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssignmentsController(IAssignmentService assignmentService) : ControllerBase
{
    private string CurrentUser => User.FindFirstValue(ClaimTypes.Name) ?? "System";

    /// <summary>Get all assignments for a specific engineer</summary>
    [HttpGet("engineer/{engineerId:int}")]
    public async Task<IActionResult> GetByEngineer(int engineerId)
        => Ok(await assignmentService.GetByEngineerAsync(engineerId));

    /// <summary>Get all assignments for a specific work order</summary>
    [HttpGet("workorder/{workOrderId:int}")]
    public async Task<IActionResult> GetByWorkOrder(int workOrderId)
        => Ok(await assignmentService.GetByWorkOrderAsync(workOrderId));

    /// <summary>Assign a work order to an engineer (with conflict detection)</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentDto dto)
    {
        var result = await assignmentService.CreateAsync(dto, CurrentUser);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    /// <summary>Reschedule an assignment (history is preserved)</summary>
    [HttpPut("{id:int}/reschedule")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleAssignmentDto dto)
        => Ok(await assignmentService.RescheduleAsync(id, dto, CurrentUser));

    /// <summary>Update assignment status</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAssignmentStatusDto dto)
        => Ok(await assignmentService.UpdateStatusAsync(id, dto));

    /// <summary>Cancel an assignment</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> Cancel(int id)
        => Ok(await assignmentService.CancelAsync(id, CurrentUser));
}
