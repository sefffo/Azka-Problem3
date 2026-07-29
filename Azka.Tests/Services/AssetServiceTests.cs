using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Services.DTOs.Asset;
using Azka.Services.Exceptions;
using Azka.Services.Implementation;
using Moq;

namespace Azka.Tests.Services;

public class AssetServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGenericRepository<Asset, int>> _assetRepo = new();
    private readonly AssetService _service;

    public AssetServiceTests()
    {
        _uow.Setup(u => u.GetRepository<Asset, int>()).Returns(_assetRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _service = new AssetService(_uow.Object);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResult()
    {
        var assets = new List<Asset>
        {
            new() { Id = 1, AssetNumber = "A-001", CustomerName = "Acme", Address = "Cairo",
                    InstallationDate = DateTime.UtcNow, AssetType = Domain.Enums.AssetType.SmartElectricityMeter }
        };
        _assetRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(1);
        _assetRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(assets);

        var result = await _service.GetAllAsync(new AssetQueryDto { Page = 1, PageSize = 10 });

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Single(result.Data.Items);
    }

    [Fact]
    public async Task GetAllAsync_EmptyList_ReturnsZeroTotal()
    {
        _assetRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(0);
        _assetRepo.Setup(r => r.ListAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(new List<Asset>());

        var result = await _service.GetAllAsync(new AssetQueryDto { Page = 1, PageSize = 10 });

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Data!.TotalCount);
        Assert.Empty(result.Data.Items);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsAssetDto()
    {
        var asset = new Asset { Id = 1, AssetNumber = "A-001", CustomerName = "Acme", Address = "Cairo",
                                InstallationDate = DateTime.UtcNow, AssetType = Domain.Enums.AssetType.SmartElectricityMeter };
        _assetRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asset);

        var result = await _service.GetByIdAsync(1);

        Assert.True(result.Succeeded);
        Assert.Equal("A-001", result.Data!.AssetNumber);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _assetRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Asset?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(99));
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenDuplicateAssetNumber_ThrowsConflict()
    {
        _assetRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(1);

        var dto = new CreateAssetDto { AssetNumber = "A-001", AssetType = Domain.Enums.AssetType.SmartElectricityMeter,
                                       Address = "Cairo", CustomerName = "Acme", InstallationDate = DateTime.UtcNow };

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_WhenUnique_AddsAssetAndSaves()
    {
        _assetRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(0);
        Asset? saved = null;
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>()))
            .Callback<Asset>(a => saved = a)
            .Returns(Task.CompletedTask);

        var dto = new CreateAssetDto { AssetNumber = "A-002", AssetType = Domain.Enums.AssetType.SmartWaterMeter,
                                       Address = "Alexandria", CustomerName = "Beta", InstallationDate = DateTime.UtcNow };

        var result = await _service.CreateAsync(dto);

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.Equal("A-002", saved!.AssetNumber);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Success_MapsAllFields()
    {
        _assetRepo.Setup(r => r.CountAsync(It.IsAny<ISpecification<Asset>>())).ReturnsAsync(0);
        _assetRepo.Setup(r => r.AddAsync(It.IsAny<Asset>())).Returns(Task.CompletedTask);

        var date = new DateTime(2024, 1, 15);
        var dto = new CreateAssetDto
        {
            AssetNumber = "A-003", AssetType = Domain.Enums.AssetType.SmartGasMeter,
            Address = "Giza", CustomerName = "Gamma", Latitude = 30.1, Longitude = 31.2,
            InstallationDate = date
        };

        var result = await _service.CreateAsync(dto);

        Assert.True(result.Succeeded);
        Assert.Equal("A-003", result.Data!.AssetNumber);
        Assert.Equal(30.1, result.Data.Latitude);
        Assert.Equal(31.2, result.Data.Longitude);
        Assert.Equal(date, result.Data.InstallationDate);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _assetRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Asset?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(99));
    }

    [Fact]
    public async Task DeleteAsync_WhenFound_DeletesAndSaves()
    {
        var asset = new Asset { Id = 5, AssetNumber = "A-005", CustomerName = "Delta", Address = "Suez",
                                InstallationDate = DateTime.UtcNow, AssetType = Domain.Enums.AssetType.SmartElectricityMeter };
        _assetRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(asset);

        var result = await _service.DeleteAsync(5);

        Assert.True(result.Succeeded);
        Assert.True(result.Data);
        _assetRepo.Verify(r => r.Delete(asset), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }
}
