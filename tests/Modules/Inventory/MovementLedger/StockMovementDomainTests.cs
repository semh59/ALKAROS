using ALKAROS.Inventory.StockMaster;
using ALKAROS.Recipes.Units;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.MovementLedger.Tests;

public sealed class StockMovementDomainTests
{
    [Fact]
    public void CreateStockMovementWithValidParametersCalculatesCorrectEffect()
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        // In movement (e.g. PurchaseReceipt)
        var inMovement = StockMovement.Create(
            itemId, locId, StockMovementType.PurchaseReceipt, 15.5m, "kg", StockMovementSourceType.PurchaseOrder);

        inMovement.Direction.Should().Be(MovementDirection.In);
        inMovement.Effect.OnHandDelta.Should().Be(15.5m);
        inMovement.Effect.ReservedDelta.Should().Be(0m);
        inMovement.Effect.AvailableDelta.Should().Be(15.5m);

        // Out movement (e.g. Consumption)
        var outMovement = StockMovement.Create(
            itemId, locId, StockMovementType.Consumption, 3.2m, "kg", StockMovementSourceType.Order);

        outMovement.Direction.Should().Be(MovementDirection.Out);
        outMovement.Effect.OnHandDelta.Should().Be(-3.2m);
        outMovement.Effect.ReservedDelta.Should().Be(0m);
        outMovement.Effect.AvailableDelta.Should().Be(-3.2m);

        // Reserve movement
        var reserveMovement = StockMovement.Create(
            itemId, locId, StockMovementType.Reservation, 2.0m, "kg", StockMovementSourceType.DailyMenu);

        reserveMovement.Direction.Should().Be(MovementDirection.Reserve);
        reserveMovement.Effect.OnHandDelta.Should().Be(0m);
        reserveMovement.Effect.ReservedDelta.Should().Be(2.0m);
        reserveMovement.Effect.AvailableDelta.Should().Be(-2.0m);

        // Release movement
        var releaseMovement = StockMovement.Create(
            itemId, locId, StockMovementType.Release, 2.0m, "kg", StockMovementSourceType.DailyMenu);

        releaseMovement.Direction.Should().Be(MovementDirection.Release);
        releaseMovement.Effect.OnHandDelta.Should().Be(0m);
        releaseMovement.Effect.ReservedDelta.Should().Be(-2.0m);
        releaseMovement.Effect.AvailableDelta.Should().Be(2.0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.0001)]
    public void PositiveMagnitudeRuleRejectsZeroOrNegativeQuantity(decimal invalidQuantity)
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var act = () => StockMovement.Create(
            itemId, locId, StockMovementType.PurchaseReceipt, invalidQuantity, "kg", StockMovementSourceType.PurchaseOrder);

        act.Should().Throw<InvalidStockMovementException>()
            .WithMessage("*strictly greater than zero*");
    }

    [Fact]
    public void MovementTypeResolvesDefaultDirection()
    {
        StockMovement.ResolveDefaultDirection(StockMovementType.PurchaseReceipt).Should().Be(MovementDirection.In);
        StockMovement.ResolveDefaultDirection(StockMovementType.ProductionOutput).Should().Be(MovementDirection.In);
        StockMovement.ResolveDefaultDirection(StockMovementType.Return).Should().Be(MovementDirection.In);
        StockMovement.ResolveDefaultDirection(StockMovementType.Consumption).Should().Be(MovementDirection.Out);
        StockMovement.ResolveDefaultDirection(StockMovementType.Waste).Should().Be(MovementDirection.Out);
        StockMovement.ResolveDefaultDirection(StockMovementType.Reservation).Should().Be(MovementDirection.Reserve);
        StockMovement.ResolveDefaultDirection(StockMovementType.Release).Should().Be(MovementDirection.Release);
    }

    [Fact]
    public void AdjustmentRequiresExplicitInOrOutDirection()
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var actReserve = () => StockMovement.Create(
            itemId, locId, StockMovementType.Adjustment, 5m, "kg", StockMovementSourceType.InventoryAudit, direction: MovementDirection.Reserve);

        actReserve.Should().Throw<InvalidStockMovementException>()
            .WithMessage("*Adjustment movement direction must be either In or Out*");

        var validIn = StockMovement.Create(
            itemId, locId, StockMovementType.Adjustment, 5m, "kg", StockMovementSourceType.InventoryAudit, direction: MovementDirection.In);
        validIn.Direction.Should().Be(MovementDirection.In);

        var validOut = StockMovement.Create(
            itemId, locId, StockMovementType.Adjustment, 5m, "kg", StockMovementSourceType.InventoryAudit, direction: MovementDirection.Out);
        validOut.Direction.Should().Be(MovementDirection.Out);
    }

    [Fact]
    public void ReversalInvertsDirectionCorrectly()
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var original = StockMovement.Create(
            itemId, locId, StockMovementType.Consumption, 4.5m, "kg", StockMovementSourceType.Order);

        var reversal = StockMovement.CreateReversal(original, reason: "Order cancelled");

        reversal.MovementType.Should().Be(StockMovementType.Reversal);
        reversal.Direction.Should().Be(MovementDirection.In);
        reversal.Quantity.Should().Be(4.5m);
        reversal.SourceType.Should().Be(StockMovementSourceType.StockMovement);
        reversal.SourceReferenceId.Should().Be(original.Id);
        reversal.Reason.Should().Be("Order cancelled");
        reversal.Effect.OnHandDelta.Should().Be(4.5m);
    }

    [Fact]
    public void ReversingAReversalThrowsInvalidStockMovementException()
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var original = StockMovement.Create(
            itemId, locId, StockMovementType.Consumption, 4.5m, "kg", StockMovementSourceType.Order);
        var reversal = StockMovement.CreateReversal(original);

        var act = () => StockMovement.CreateReversal(reversal);

        act.Should().Throw<InvalidStockMovementException>()
            .WithMessage("*Cannot reverse a reversal*");
    }

    [Fact]
    public void InvalidSourceTypeThrowsInvalidStockMovementException()
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var act = () => StockMovement.Create(
            itemId, locId, StockMovementType.PurchaseReceipt, 10m, "kg", "NonExistentSource");

        act.Should().Throw<InvalidStockMovementException>()
            .WithMessage("*Invalid source type*");
    }

    [Fact]
    public async Task StockMovementServiceRejectsInactiveItemOrLocation()
    {
        var itemRepo = new FakeStockItemRepository();
        var locRepo = new FakeStockLocationRepository();
        var movementRepo = new FakeStockMovementRepository();
        var service = new StockMovementService(movementRepo, itemRepo, locRepo, new UnitConverter());

        var activeLoc = StockLocation.Create("LOC-ACT", "Active Loc", StockLocationType.Warehouse);
        await locRepo.AddAsync(activeLoc);

        var inactiveItem = StockItem.Create("SKU-INA", "Inactive Item", StockItemType.RawMaterial, "kg", isActive: false);
        await itemRepo.AddAsync(inactiveItem);

        var actInactiveItem = () => service.RecordMovementAsync(
            inactiveItem.Id, activeLoc.Id, StockMovementType.PurchaseReceipt, 10m, "kg", StockMovementSourceType.PurchaseOrder);

        await actInactiveItem.Should().ThrowAsync<InactiveStockItemException>();

        var activeItem = StockItem.Create("SKU-ACT", "Active Item", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(activeItem);

        var inactiveLoc = StockLocation.Create("LOC-INA", "Inactive Loc", StockLocationType.Warehouse, isActive: false);
        await locRepo.AddAsync(inactiveLoc);

        var actInactiveLoc = () => service.RecordMovementAsync(
            activeItem.Id, inactiveLoc.Id, StockMovementType.PurchaseReceipt, 10m, "kg", StockMovementSourceType.PurchaseOrder);

        await actInactiveLoc.Should().ThrowAsync<InactiveStockLocationException>();
    }

    [Fact]
    public async Task StockMovementServiceRejectsIncompatibleUnitDimension()
    {
        var itemRepo = new FakeStockItemRepository();
        var locRepo = new FakeStockLocationRepository();
        var movementRepo = new FakeStockMovementRepository();
        var service = new StockMovementService(movementRepo, itemRepo, locRepo, new UnitConverter());

        var loc = StockLocation.Create("LOC-1", "Warehouse", StockLocationType.Warehouse);
        await locRepo.AddAsync(loc);

        var item = StockItem.Create("SKU-FLOUR", "Flour", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);

        // Attempting to move flour in "liter" (Mass vs Volume)
        var act = () => service.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 5m, "l", StockMovementSourceType.PurchaseOrder);

        await act.Should().ThrowAsync<IncompatibleUnitDimensionException>();
    }

    [Fact]
    public async Task StockMovementServiceReversalChecksDuplicateReversal()
    {
        var itemRepo = new FakeStockItemRepository();
        var locRepo = new FakeStockLocationRepository();
        var movementRepo = new FakeStockMovementRepository();
        var service = new StockMovementService(movementRepo, itemRepo, locRepo, new UnitConverter());

        var loc = StockLocation.Create("LOC-1", "Warehouse", StockLocationType.Warehouse);
        await locRepo.AddAsync(loc);

        var item = StockItem.Create("SKU-1", "Sugar", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);

        var movement = await service.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 20m, "kg", StockMovementSourceType.PurchaseOrder);

        // First reversal succeeds
        var reversal = await service.ReverseMovementAsync(movement.Id, "Wrong order");
        reversal.Should().NotBeNull();
        reversal.MovementType.Should().Be(StockMovementType.Reversal);

        // Second reversal must fail
        var actSecond = () => service.ReverseMovementAsync(movement.Id, "Wrong order again");
        await actSecond.Should().ThrowAsync<DuplicateReversalException>()
            .WithMessage("*already been reversed*");
    }

    private sealed class FakeStockMovementRepository : IStockMovementRepository
    {
        private readonly List<StockMovement> _movements = new();

        public Task AppendAsync(StockMovement movement, CancellationToken ct = default)
        {
            _movements.Add(movement);
            return Task.CompletedTask;
        }

        public Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_movements.FirstOrDefault(m => m.Id == id));

        public Task<IReadOnlyList<StockMovement>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockMovement>>(_movements.Where(m => m.StockItemId == stockItemId).ToList());

        public Task<IReadOnlyList<StockMovement>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockMovement>>(_movements.Where(m => m.StockLocationId == stockLocationId).ToList());

        public Task<IReadOnlyList<StockMovement>> GetBySourceAsync(string sourceType, Guid sourceReferenceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockMovement>>(
                _movements.Where(m => string.Equals(m.SourceType, sourceType, StringComparison.OrdinalIgnoreCase) && m.SourceReferenceId == sourceReferenceId).ToList());

        public Task<bool> HasReversalAsync(Guid stockMovementId, CancellationToken ct = default)
            => Task.FromResult(_movements.Any(m => m.MovementType == StockMovementType.Reversal && m.SourceReferenceId == stockMovementId));
    }

    private sealed class FakeStockItemRepository : IStockItemRepository
    {
        private readonly Dictionary<Guid, StockItem> _items = new();

        public Task AddAsync(StockItem item, CancellationToken ct = default)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.TryGetValue(id, out var it) ? it : null);

        public Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(_items.Values.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.Where(i => !activeOnly || i.IsActive).ToList());

        public Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.Where(i => i.ItemType == itemType && (!activeOnly || i.IsActive)).ToList());

        public Task UpdateAsync(StockItem item, CancellationToken ct = default)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStockLocationRepository : IStockLocationRepository
    {
        private readonly Dictionary<Guid, StockLocation> _locations = new();

        public Task AddAsync(StockLocation location, CancellationToken ct = default)
        {
            _locations[location.Id] = location;
            return Task.CompletedTask;
        }

        public Task<StockLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_locations.TryGetValue(id, out var loc) ? loc : null);

        public Task<StockLocation?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(_locations.Values.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<StockLocation>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockLocation>>(_locations.Values.Where(l => !activeOnly || l.IsActive).ToList());

        public Task UpdateAsync(StockLocation location, CancellationToken ct = default)
        {
            _locations[location.Id] = location;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _locations.Remove(id);
            return Task.CompletedTask;
        }
    }
}
