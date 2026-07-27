using Azka.Services.DTOs.Engineer;
using Azka.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Azka.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EngineersController(IEngineerService engineerService) : ControllerBase
{
    /// <summary>Get all active engineers</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await engineerService.GetAllAsync());

    /// <summary>Get engineer by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await engineerService.GetByIdAsync(id));

    /// <summary>Get engineers by region</summary>
    [HttpGet("region/{region}")]
    public async Task<IActionResult> GetByRegion(string region)
        => Ok(await engineerService.GetByRegionAsync(region));

    /// <summary>Get engineer workload for a specific date</summary>
    [HttpGet("{id:int}/workload")]
    public async Task<IActionResult> GetWorkload(int id, [FromQuery] DateTime date)
        => Ok(await engineerService.GetWorkloadAsync(id, date));

    /// <summary>Create a new engineer (Admin only)</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateEngineerDto dto)
    {
        var result = await engineerService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>Update an existing engineer (Admin only)</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEngineerDto dto)
        => Ok(await engineerService.UpdateAsync(id, dto));

    /// <summary>Deactivate an engineer (Admin only)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await engineerService.DeleteAsync(id));
}
