using ALKAROS.Measurements;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.StockMaster.Tests;

public sealed class StockMasterDomainTests
{
    [Fact]
    public void CreateStockLocationWithValidParametersNormalizesCode()
    {
        var loc = StockLocation.Create("wh-main", "Main Warehouse", StockLocationType.Warehouse);

        loc.Code.Should().Be("WH-MAIN");
        loc.Name.Should().Be("Main Warehouse");
        loc.LocationType.Should().Be(StockLocationType.Warehouse);
        loc.IsActive.Should().BeTrue();
        loc.RowVersion.Should().Be(1);
    }

    [Fact]
    public void InactiveStockLocationThrowsInactiveStockLocationExceptionOnMovement()
    {
        var loc = StockLocation.Create("KIT-01", "Kitchen 1", StockLocationType.Kitchen, isActive: false);

        var act = () => loc.EnsureActiveForMovement();

        act.Should().Throw<InactiveStockLocationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void CreateStockItemWithValidParametersNormalizesCodeAndUnit()
    {
        var item = StockItem.Create("sku-flour-01", "Wheat Flour Type 00", StockItemType.RawMaterial, "KG");

        item.Code.Should().Be("SKU-FLOUR-01");
        item.Name.Should().Be("Wheat Flour Type 00");
        item.ItemType.Should().Be(StockItemType.RawMaterial);
        item.TrackingUnitCode.Should().Be("kg");
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public void InactiveStockItemThrowsInactiveStockItemExceptionOnMovement()
    {
        var item = StockItem.Create("SKU-OIL", "Olive Oil", StockItemType.RawMaterial, "l", isActive: false);

        var act = () => item.EnsureActiveForMovement();

        act.Should().Throw<InactiveStockItemException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void ProductStockMappingRequiresPositiveMultiplier()
    {
        var prodId = Guid.NewGuid();
        var stockId = Guid.NewGuid();

        var act = () => new ProductStockMapping(prodId, stockId, 0m);

        act.Should().Throw<InvalidProductStockMappingException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void StockMasterServiceRejectsUnknownTrackingUnit()
    {
        var unitConverter = new UnitConverter();
        var service = new StockMasterService(
            new FakeLocationRepository(),
            new FakeItemRepository(),
            new FakeMappingRepository(),
            unitConverter);

        var act = () => service.CreateStockItemAsync(
            code: "SKU-TEST",
            name: "Test Item",
            itemType: StockItemType.RawMaterial,
            trackingUnitCode: "nonexistent_unit_xyz");

        act.Should().ThrowAsync<UnknownUnitException>();
    }

    [Fact]
    public void StockMasterServiceRejectsInactiveDefaultLocation()
    {
        var locRepo = new FakeLocationRepository();
        var inactiveLoc = StockLocation.Create("LOC-OFF", "Offline Store", StockLocationType.DryStorage, isActive: false);
        locRepo.Items[inactiveLoc.Id] = inactiveLoc;
        locRepo.ItemsByCode[inactiveLoc.Code] = inactiveLoc;

        var service = new StockMasterService(
            locRepo,
            new FakeItemRepository(),
            new FakeMappingRepository(),
            new UnitConverter());

        var act = () => service.CreateStockItemAsync(
            code: "SKU-DRY",
            name: "Dry Beans",
            itemType: StockItemType.RawMaterial,
            trackingUnitCode: "kg",
            defaultLocationId: inactiveLoc.Id);

        act.Should().ThrowAsync<InactiveStockLocationException>()
            .WithMessage("*Cannot set inactive stock location*");
    }

    private sealed class FakeLocationRepository : IStockLocationRepository
    {
        public Dictionary<Guid, StockLocation> Items { get; } = new();
        public Dictionary<string, StockLocation> ItemsByCode { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task AddAsync(StockLocation location, CancellationToken ct = default)
        {
            Items[location.Id] = location;
            ItemsByCode[location.Code] = location;
            return Task.CompletedTask;
        }

        public Task<StockLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(id, out var loc) ? loc : null);

        public Task<StockLocation?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(ItemsByCode.TryGetValue(code, out var loc) ? loc : null);

        public Task<IReadOnlyList<StockLocation>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockLocation>>(Items.Values.Where(l => !activeOnly || l.IsActive).ToList());

        public Task UpdateAsync(StockLocation location, CancellationToken ct = default)
        {
            Items[location.Id] = location;
            ItemsByCode[location.Code] = location;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (Items.Remove(id, out var loc))
                ItemsByCode.Remove(loc.Code);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeItemRepository : IStockItemRepository
    {
        public Dictionary<Guid, StockItem> Items { get; } = new();
        public Dictionary<string, StockItem> ItemsByCode { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task AddAsync(StockItem item, CancellationToken ct = default)
        {
            Items[item.Id] = item;
            ItemsByCode[item.Code] = item;
            return Task.CompletedTask;
        }

        public Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(id, out var item) ? item : null);

        public Task<IReadOnlyList<StockItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(Items.Values.Where(i => ids.Contains(i.Id)).ToList());

        public Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(ItemsByCode.TryGetValue(code, out var item) ? item : null);

        public Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(Items.Values.Where(i => !activeOnly || i.IsActive).ToList());

        public Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(Items.Values.Where(i => i.ItemType == itemType && (!activeOnly || i.IsActive)).ToList());

        public Task UpdateAsync(StockItem item, CancellationToken ct = default)
        {
            Items[item.Id] = item;
            ItemsByCode[item.Code] = item;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (Items.Remove(id, out var item))
                ItemsByCode.Remove(item.Code);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMappingRepository : IProductStockMappingRepository
    {
        public List<ProductStockMapping> Mappings { get; } = new();

        public Task AddOrUpdateAsync(ProductStockMapping mapping, CancellationToken ct = default)
        {
            Mappings.RemoveAll(m => m.ProductId == mapping.ProductId && m.StockItemId == mapping.StockItemId);
            Mappings.Add(mapping);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProductStockMapping>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductStockMapping>>(Mappings.Where(m => m.ProductId == productId).ToList());

        public Task<IReadOnlyList<ProductStockMapping>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductStockMapping>>(Mappings.Where(m => productIds.Contains(m.ProductId)).ToList());

        public Task<IReadOnlyList<ProductStockMapping>> GetByStockItemIdAsync(Guid stockItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductStockMapping>>(Mappings.Where(m => m.StockItemId == stockItemId).ToList());

        public Task RemoveAsync(Guid productId, Guid stockItemId, CancellationToken ct = default)
        {
            Mappings.RemoveAll(m => m.ProductId == productId && m.StockItemId == stockItemId);
            return Task.CompletedTask;
        }
    }
}
