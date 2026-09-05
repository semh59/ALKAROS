using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Recipes.Units;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.ManualAdjustments.Tests;

public sealed class ManualAdjustmentDomainTests
{
    [Fact]
    public async Task IncreaseAdjustmentCreatesInMovementAndUpdatesBalance()
    {
        var (service, movementRepo, itemRepo, locRepo, balanceRepo, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-ADJ-1", "Coffee Beans", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-ADJ-1", "Main Bar", StockLocationType.Bar);
        await locRepo.AddAsync(loc);

        var managerId = Guid.NewGuid();
        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Increase,
            Quantity: 5m,
            UnitCode: "kg",
            Reason: "Stock count surplus after audit",
            AuthorizedBy: managerId);

        var result = await service.AdjustInventoryAsync(request);

        result.Should().NotBeNull();
        result.Movement.MovementType.Should().Be(StockMovementType.Adjustment);
        result.Movement.Direction.Should().Be(MovementDirection.In);
        result.Movement.Quantity.Should().Be(5m);
        result.Movement.UnitCode.Should().Be("kg");
        result.Movement.SourceType.Should().Be(StockMovementSourceType.InventoryAudit);
        result.Movement.Reason.Should().Be("Stock count surplus after audit");
        result.Movement.CreatedBy.Should().Be(managerId);

        result.PreviousOnHandQuantity.Should().Be(0m);
        result.NewOnHandQuantity.Should().Be(5m);
        result.UpdatedBalance.OnHandQuantity.Should().Be(5m);
    }

    [Fact]
    public async Task DecreaseAdjustmentCreatesOutMovementAndUpdatesBalance()
    {
        var (service, movementRepo, itemRepo, locRepo, balanceRepo, projector) = CreateTestHarness();

        var item = StockItem.Create("SKU-ADJ-2", "Milk", StockItemType.RawMaterial, "l");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-ADJ-2", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        // Pre-existing balance = 20l
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 20m);

        var managerId = Guid.NewGuid();
        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Decrease,
            Quantity: 4m,
            UnitCode: "l",
            Reason: "Spillage during restocking",
            AuthorizedBy: managerId);

        var result = await service.AdjustInventoryAsync(request);

        result.Movement.MovementType.Should().Be(StockMovementType.Adjustment);
        result.Movement.Direction.Should().Be(MovementDirection.Out);
        result.Movement.Quantity.Should().Be(4m);
        result.PreviousOnHandQuantity.Should().Be(20m);
        result.NewOnHandQuantity.Should().Be(16m);
    }

    [Fact]
    public async Task DecreaseAdjustmentExceedingOnHandThrowsNegativeInventoryResultException()
    {
        var (service, _, itemRepo, locRepo, balanceRepo, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-ADJ-3", "Cheese", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-ADJ-3", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        // Current balance = 5kg
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 5m);

        // Attempting to adjust down by 8kg (result would be -3kg)
        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Decrease,
            Quantity: 8m,
            UnitCode: "kg",
            Reason: "Inventory variance",
            AuthorizedBy: Guid.NewGuid());

        var act = () => service.AdjustInventoryAsync(request);

        await act.Should().ThrowAsync<NegativeInventoryResultException>()
            .WithMessage("*negative on-hand balance*");
    }

    [Fact]
    public async Task AdjustmentWithoutReasonThrowsInvalidAdjustmentReasonException()
    {
        var (service, _, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-ADJ-4", "Tea", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-ADJ-4", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Increase,
            Quantity: 1m,
            UnitCode: "kg",
            Reason: "   ",
            AuthorizedBy: Guid.NewGuid());

        var act = () => service.AdjustInventoryAsync(request);

        await act.Should().ThrowAsync<InvalidAdjustmentReasonException>()
            .WithMessage("*reason is mandatory*");
    }

    [Fact]
    public async Task AdjustmentWithoutAuthorizedByThrowsUnauthorizedAdjustmentException()
    {
        var (service, _, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-ADJ-5", "Flour", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-ADJ-5", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Increase,
            Quantity: 2m,
            UnitCode: "kg",
            Reason: "Valid reason",
            AuthorizedBy: Guid.Empty);

        var act = () => service.AdjustInventoryAsync(request);

        await act.Should().ThrowAsync<UnauthorizedAdjustmentException>()
            .WithMessage("*authorized operator*");
    }

    [Fact]
    public async Task AdjustmentWithNonPositiveQuantityThrowsInvalidAdjustmentQuantityException()
    {
        var (service, _, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-ADJ-6", "Salt", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-ADJ-6", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Increase,
            Quantity: 0m,
            UnitCode: "kg",
            Reason: "Valid reason",
            AuthorizedBy: Guid.NewGuid());

        var act = () => service.AdjustInventoryAsync(request);

        await act.Should().ThrowAsync<InvalidAdjustmentQuantityException>()
            .WithMessage("*strictly positive*");
    }

    [Fact]
    public async Task AdjustmentConvertsCompatibleUnitsAccurately()
    {
        var (service, _, itemRepo, locRepo, balanceRepo, _) = CreateTestHarness();

        // Item tracked in kg
        var item = StockItem.Create("SKU-ADJ-7", "Sugar", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-ADJ-7", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        // Adjusting 500 grams (should be 0.5 kg in tracking unit)
        var request = new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Increase,
            Quantity: 500m,
            UnitCode: "g",
            Reason: "Small scale surplus",
            AuthorizedBy: Guid.NewGuid());

        var result = await service.AdjustInventoryAsync(request);

        result.Movement.Quantity.Should().Be(0.5m);
        result.Movement.UnitCode.Should().Be("kg");
        result.NewOnHandQuantity.Should().Be(0.5m);
    }

    private static (
        InventoryAdjustmentService service,
        FakeStockMovementRepository movementRepo,
        FakeStockItemRepository itemRepo,
        FakeStockLocationRepository locRepo,
        FakeStockBalanceRepository balanceRepo,
        StockBalanceProjector projector) CreateTestHarness()
    {
        var movementRepo = new FakeStockMovementRepository();
        var itemRepo = new FakeStockItemRepository();
        var locRepo = new FakeStockLocationRepository();
        var balanceRepo = new FakeStockBalanceRepository();
        var projector = new StockBalanceProjector(balanceRepo, movementRepo, locRepo);
        var unitConverter = new UnitConverter();

        var service = new InventoryAdjustmentService(
            movementRepo, itemRepo, locRepo, balanceRepo, projector, unitConverter);

        return (service, movementRepo, itemRepo, locRepo, balanceRepo, projector);
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

    private sealed class FakeStockBalanceRepository : IStockBalanceRepository
    {
        private readonly Dictionary<(Guid, Guid), StockBalance> _balances = new();

        public Task<StockBalance?> GetByItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default)
        {
            _balances.TryGetValue((stockItemId, stockLocationId), out var b);
            return Task.FromResult(b);
        }

        public Task<IReadOnlyList<StockBalance>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.Where(b => b.StockItemId == stockItemId).ToList());

        public Task<IReadOnlyList<StockBalance>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.Where(b => b.StockLocationId == stockLocationId).ToList());

        public Task<IReadOnlyList<StockBalance>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.ToList());

        public Task<StockBalance> ApplyOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, CancellationToken ct = default)
        {
            if (!_balances.TryGetValue((stockItemId, stockLocationId), out var bal))
            {
                bal = StockBalance.Create(stockItemId, stockLocationId, onHandDelta);
                _balances[(stockItemId, stockLocationId)] = bal;
            }
            else
            {
                bal.ApplyOnHandDelta(onHandDelta);
                bal = new StockBalance(bal.Id, bal.StockItemId, bal.StockLocationId, bal.OnHandQuantity, bal.ReservedQuantity, bal.AvailableQuantity, bal.UpdatedAt, bal.RowVersion + 1);
                _balances[(stockItemId, stockLocationId)] = bal;
            }
            return Task.FromResult(bal);
        }

        public Task SetExactBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal onHandQuantity, CancellationToken ct = default)
        {
            if (!_balances.TryGetValue((stockItemId, stockLocationId), out var bal))
            {
                bal = StockBalance.Create(stockItemId, stockLocationId, onHandQuantity);
                _balances[(stockItemId, stockLocationId)] = bal;
            }
            else
            {
                bal.SetOnHand(onHandQuantity);
                bal = new StockBalance(bal.Id, bal.StockItemId, bal.StockLocationId, bal.OnHandQuantity, bal.ReservedQuantity, bal.AvailableQuantity, bal.UpdatedAt, bal.RowVersion + 1);
                _balances[(stockItemId, stockLocationId)] = bal;
            }
            return Task.CompletedTask;
        }

        public Task ResetAllBalancesAsync(CancellationToken ct = default)
        {
            _balances.Clear();
            return Task.CompletedTask;
        }
    }
}
