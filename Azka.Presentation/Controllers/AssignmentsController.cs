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

    /// <summary>
    /// Get assignments. All query params are optional.
    /// Filter by engineerId, workOrderId, status, fromDate, toDate or any combination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AssignmentQueryDto query)
        => Ok(await assignmentService.GetAllAsync(query));

    /// <summary>Get a single assignment by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await assignmentService.GetByIdAsync(id));

    /// <summary>Assign a work order to an engineer (with conflict and capacity checks)</summary>
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

    /// <summary>Update assignment status (Engineer can mark in-progress/completed)</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin,Dispatcher,Engineer")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAssignmentStatusDto dto)
        => Ok(await assignmentService.UpdateStatusAsync(id, dto));

    /// <summary>Cancel an assignment</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> Cancel(int id)
        => Ok(await assignmentService.CancelAsync(id, CurrentUser));
}
