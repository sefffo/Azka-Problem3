using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assets;
using Azka.Services.DTOs.Asset;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;

namespace Azka.Services.Implementation;

public class AssetService(IUnitOfWork unitOfWork) : IAssetService
{
    public async Task<ApiResponse<PagedResult<AssetDto>>> GetAllAsync(AssetQueryDto q)
    {
        var repo = unitOfWork.GetRepository<Asset, int>();

        var countSpec = new AssetQuerySpecification(
            q.AssetType, q.Status, q.CustomerName, q.AssetNumber, countOnly: true);

        var dataSpec = new AssetQuerySpecification(
            q.AssetType, q.Status, q.CustomerName, q.AssetNumber, q.Page, q.PageSize);

        var total = await repo.CountAsync(countSpec);
        var items = await repo.ListAsync(dataSpec);

        return ApiResponse<PagedResult<AssetDto>>.Success(new PagedResult<AssetDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = q.Page,
            PageSize   = q.PageSize
        });
    }

    public async Task<ApiResponse<AssetDto>> GetByIdAsync(int id)
    {
        var asset = await unitOfWork.GetRepository<Asset, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Asset), id);
        return ApiResponse<AssetDto>.Success(MapToDto(asset));
    }

    public async Task<ApiResponse<AssetDto>> CreateAsync(CreateAssetDto dto)
    {
        var duplicateSpec = new AssetByNumberSpecification(dto.AssetNumber);
        if (await unitOfWork.GetRepository<Asset, int>().CountAsync(duplicateSpec) > 0)
            throw new ConflictException($"Asset number '{dto.AssetNumber}' is already registered.");

        var asset = new Asset
        {
            AssetNumber      = dto.AssetNumber,
            AssetType        = dto.AssetType,
            Address          = dto.Address,
            Latitude         = dto.Latitude,
            Longitude        = dto.Longitude,
            CustomerName     = dto.CustomerName,
            InstallationDate = dto.InstallationDate
        };

        await unitOfWork.GetRepository<Asset, int>().AddAsync(asset);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<AssetDto>.Success(MapToDto(asset), "Asset registered successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var asset = await unitOfWork.GetRepository<Asset, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Asset), id);

        unitOfWork.GetRepository<Asset, int>().Delete(asset);
        await unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Asset deleted successfully.");
    }

    private static AssetDto MapToDto(Asset a) => new()
    {
        Id               = a.Id,
        AssetNumber      = a.AssetNumber,
        AssetType        = a.AssetType.ToString(),
        Address          = a.Address,
        Latitude         = a.Latitude,
        Longitude        = a.Longitude,
        CustomerName     = a.CustomerName,
        Status           = a.Status.ToString(),
        InstallationDate = a.InstallationDate
    };
}
