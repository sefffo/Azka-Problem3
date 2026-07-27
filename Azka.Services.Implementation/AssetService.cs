using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Persistence.Data;
using Azka.Services.DTOs.Asset;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Azka.Services.Implementation;

public class AssetService(
    IUnitOfWork unitOfWork,
    AppDbContext context) : IAssetService
{
    public async Task<ApiResponse<IEnumerable<AssetDto>>> GetAllAsync()
    {
        var assets = await context.Assets
            .AsNoTracking()
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();

        return ApiResponse<IEnumerable<AssetDto>>.Success(assets.Select(MapToDto));
    }

    public async Task<ApiResponse<AssetDto>> GetByIdAsync(int id)
    {
        var asset = await context.Assets.FindAsync(id)
            ?? throw new NotFoundException(nameof(Asset), id);

        return ApiResponse<AssetDto>.Success(MapToDto(asset));
    }

    public async Task<ApiResponse<AssetDto>> CreateAsync(CreateAssetDto dto)
    {
        var exists = await context.Assets.AnyAsync(a => a.AssetNumber == dto.AssetNumber);
        if (exists)
            throw new ConflictException($"Asset number '{dto.AssetNumber}' is already registered.");

        var asset = new Asset
        {
            AssetNumber = dto.AssetNumber,
            AssetType = dto.AssetType,
            Address = dto.Address,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            CustomerName = dto.CustomerName,
            InstallationDate = dto.InstallationDate
        };

        var repo = unitOfWork.GetRepository<Asset, int>();
        await repo.AddAsync(asset);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<AssetDto>.Success(MapToDto(asset), "Asset registered successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var asset = await context.Assets.FindAsync(id)
            ?? throw new NotFoundException(nameof(Asset), id);

        var repo = unitOfWork.GetRepository<Asset, int>();
        repo.Remove(asset);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Asset deleted successfully.");
    }

    private static AssetDto MapToDto(Asset a) => new()
    {
        Id = a.Id,
        AssetNumber = a.AssetNumber,
        AssetType = a.AssetType.ToString(),
        Address = a.Address,
        Latitude = a.Latitude,
        Longitude = a.Longitude,
        CustomerName = a.CustomerName,
        Status = a.Status.ToString(),
        InstallationDate = a.InstallationDate
    };
}
