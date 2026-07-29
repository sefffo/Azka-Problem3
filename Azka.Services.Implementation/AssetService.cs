using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications.Assets;
using Azka.Services.DTOs.Asset;
using Azka.Services.Exceptions;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.Extensions.Caching.Memory;

namespace Azka.Services.Implementation;

public class AssetService(
    IUnitOfWork unitOfWork,
    IMemoryCache cache,
    DashboardService dashboardService) : IAssetService
{
    // Asset reference data is very stable — 10-minute TTL.
    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromMinutes(10);

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<AssetDto>>> GetAllAsync(AssetQueryDto q)
    {
        var cacheKey = CacheKeys.AssetListPrefix +
                       $"{q.AssetType}_{q.Status}_{q.CustomerName}_{q.AssetNumber}_{q.Page}_{q.PageSize}";

        if (cache.TryGetValue(cacheKey, out PagedResult<AssetDto>? cached))
            return ApiResponse<PagedResult<AssetDto>>.Success(cached!);

        var repo = unitOfWork.GetRepository<Asset, int>();

        var countSpec = new AssetQuerySpecification(
            q.AssetType, q.Status, q.CustomerName, q.AssetNumber, countOnly: true);
        var dataSpec  = new AssetQuerySpecification(
            q.AssetType, q.Status, q.CustomerName, q.AssetNumber, q.Page, q.PageSize);

        var total = await repo.CountAsync(countSpec);
        var items = await repo.ListAsync(dataSpec);

        var result = new PagedResult<AssetDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = q.Page,
            PageSize   = q.PageSize
        };

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ListCacheTtl,
            Size = 1
        });

        return ApiResponse<PagedResult<AssetDto>>.Success(result);
    }

    public async Task<ApiResponse<AssetDto>> GetByIdAsync(int id)
    {
        var asset = await unitOfWork.GetRepository<Asset, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Asset), id);
        return ApiResponse<AssetDto>.Success(MapToDto(asset));
    }

    // ── Writes (all invalidate asset list cache + dashboard cache) ───────────

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

        InvalidateAssetCaches();

        return ApiResponse<AssetDto>.Success(MapToDto(asset), "Asset registered successfully.");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var asset = await unitOfWork.GetRepository<Asset, int>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Asset), id);

        unitOfWork.GetRepository<Asset, int>().Delete(asset);
        await unitOfWork.SaveChangesAsync();

        InvalidateAssetCaches();

        return ApiResponse<bool>.Success(true, "Asset deleted successfully.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the asset list cache tag (expires all paged variants)
    /// and the dashboard cache.
    /// </summary>
    private void InvalidateAssetCaches()
    {
        cache.Remove(CacheKeys.AssetListPrefix + "tag");
        dashboardService.InvalidateDashboard();
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
