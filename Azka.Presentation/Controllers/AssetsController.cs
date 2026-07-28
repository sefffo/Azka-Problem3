using Azka.Services.DTOs.Asset;
using Azka.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Azka.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetsController(IAssetService assetService) : ControllerBase
{
    /// <summary>
    /// Get assets. All query params are optional.
    /// Pass assetType / status / customerName / assetNumber to filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AssetQueryDto query)
        => Ok(await assetService.GetAllAsync(query));

    /// <summary>Get a single asset by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await assetService.GetByIdAsync(id));

    /// <summary>Register a new asset (Admin only)</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateAssetDto dto)
    {
        var result = await assetService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>Delete an asset (Admin only)</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await assetService.DeleteAsync(id));
}
