using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using Npgsql;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.MovementReversal.Tests;

public sealed class StockMovementReversalDomainTests
{
    [Fact]
    public async Task ReversalRestoresExactQuantityAndInvertsDirectionForInMovement()
    {
        var (service, movementRepo, itemRepo, locRepo, balanceRepo, projector) = CreateTestHarness();

        var item = StockItem.Create("SKU-1", "Tomato", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-1", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        // In movement (PurchaseReceipt +20kg)
        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.PurchaseReceipt, 20m, "kg", StockMovementSourceType.PurchaseOrder);
        await movementRepo.AppendAsync(original);
        await projector.ApplyMovementAsync(original);

        var balBefore = await balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balBefore!.OnHandQuantity.Should().Be(20m);

        // Reversal
        var result = await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Supplier recall"));

        result.Should().NotBeNull();
        result.OriginalMovement.Id.Should().Be(original.Id);
        result.ReversalMovement.MovementType.Should().Be(StockMovementType.Reversal);
        result.ReversalMovement.Direction.Should().Be(MovementDirection.Out);
        result.ReversalMovement.Quantity.Should().Be(20m);
        result.ReversalMovement.UnitCode.Should().Be("kg");
        result.ReversalMovement.StockItemId.Should().Be(item.Id);
        result.ReversalMovement.StockLocationId.Should().Be(loc.Id);
        result.ReversalMovement.SourceType.Should().Be(StockMovementSourceType.StockMovement);
        result.ReversalMovement.SourceReferenceId.Should().Be(original.Id);
        result.ReversalMovement.Reason.Should().Be("Supplier recall");

        // Restored balance: 20 - 20 = 0
        result.RestoredBalance.OnHandQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task ReversalRestoresExactQuantityAndInvertsDirectionForOutMovement()
    {
        var (service, movementRepo, itemRepo, locRepo, balanceRepo, projector) = CreateTestHarness();

        var item = StockItem.Create("SKU-2", "Oil", StockItemType.RawMaterial, "l");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-2", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        // Out movement (Consumption -5l)
        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Consumption, 5m, "l", StockMovementSourceType.Order);
        await movementRepo.AppendAsync(original);
        await projector.ApplyMovementAsync(original);

        var balBefore = await balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balBefore!.OnHandQuantity.Should().Be(-5m);

        // Reversal
        var result = await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Order cancelled by customer"));

        result.ReversalMovement.MovementType.Should().Be(StockMovementType.Reversal);
        result.ReversalMovement.Direction.Should().Be(MovementDirection.In);
        result.ReversalMovement.Quantity.Should().Be(5m);
        result.ReversalMovement.Effect.OnHandDelta.Should().Be(5m);

        // Restored balance: -5 + 5 = 0
        result.RestoredBalance.OnHandQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task ReversalRestoresReservedQuantityForReservationMovement()
    {
        var (service, movementRepo, itemRepo, locRepo, balanceRepo, projector) = CreateTestHarness();

        var item = StockItem.Create("SKU-3", "Steak", StockItemType.RawMaterial, "piece");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-3", "Cold Store", StockLocationType.ColdStorage);
        await locRepo.AddAsync(loc);

        // Reservation movement (Reserve 3 pieces)
        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Reservation, 3m, "piece", StockMovementSourceType.DailyMenu);
        await movementRepo.AppendAsync(original);

        var result = await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Menu reservation cancelled"));

        result.ReversalMovement.MovementType.Should().Be(StockMovementType.Reversal);
        result.ReversalMovement.Direction.Should().Be(MovementDirection.Release);
        result.ReversalMovement.Quantity.Should().Be(3m);
        result.ReversalMovement.Effect.OnHandDelta.Should().Be(0m);
        result.ReversalMovement.Effect.ReservedDelta.Should().Be(-3m);
        result.ReversalMovement.Effect.AvailableDelta.Should().Be(3m);
    }

    [Fact]
    public async Task ReversalRequiresNonEmptyReason()
    {
        var (service, _, _, _, _, _) = CreateTestHarness();

        var actEmpty = () => service.ReverseMovementAsync(new StockMovementReversalRequest(Guid.NewGuid(), "   "));
        await actEmpty.Should().ThrowAsync<InvalidReversalReasonException>()
            .WithMessage("*reason cannot be empty*");
    }

    [Fact]
    public async Task ReversalRejectsEmptyGuid()
    {
        var (service, _, _, _, _, _) = CreateTestHarness();

        var actEmpty = () => service.ReverseMovementAsync(new StockMovementReversalRequest(Guid.Empty, "Valid reason"));
        await actEmpty.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Original movement ID cannot be empty*");
    }

    [Fact]
    public async Task ReversalOfNonExistentMovementThrowsStockMovementNotFoundException()
    {
        var (service, _, _, _, _, _) = CreateTestHarness();
        var missingId = Guid.NewGuid();

        var actMissing = () => service.ReverseMovementAsync(new StockMovementReversalRequest(missingId, "Valid reason"));
        await actMissing.Should().ThrowAsync<StockMovementNotFoundException>();
    }

    [Fact]
    public async Task ReversingAReversalThrowsReversalNotEligibleException()
    {
        var (service, movementRepo, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-4", "Flour", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-4", "Warehouse", StockLocationType.Warehouse);
        await locRepo.AddAsync(loc);

        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Consumption, 10m, "kg", StockMovementSourceType.Order);
        await movementRepo.AppendAsync(original);

        var firstReversal = await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "First reversal"));

        // Attempting to reverse the reversal
        var actReverseReversal = () => service.ReverseMovementAsync(new StockMovementReversalRequest(firstReversal.ReversalMovement.Id, "Reverse of reversal"));
        await actReverseReversal.Should().ThrowAsync<ReversalNotEligibleException>()
            .WithMessage("*already a Reversal*");
    }

    [Fact]
    public async Task DuplicateReversalAttemptThrowsDuplicateReversalException()
    {
        var (service, movementRepo, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-5", "Sugar", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-5", "Warehouse", StockLocationType.Warehouse);
        await locRepo.AddAsync(loc);

        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Consumption, 7m, "kg", StockMovementSourceType.Order);
        await movementRepo.AppendAsync(original);

        // First reversal succeeds
        await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Valid void"));

        // Second reversal must fail
        var actSecond = () => service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Duplicate void"));
        await actSecond.Should().ThrowAsync<DuplicateReversalException>()
            .WithMessage("*already been reversed*");
    }

    [Fact]
    public async Task ReversalPreservesOriginalMovementRecordCompletelyUnchanged()
    {
        var (service, movementRepo, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-6", "Butter", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-6", "Cold Store", StockLocationType.ColdStorage);
        await locRepo.AddAsync(loc);

        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Consumption, 4m, "kg", StockMovementSourceType.Order, reason: "Order 123");
        await movementRepo.AppendAsync(original);

        await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Reversal reason"));

        var originalAfter = await movementRepo.GetByIdAsync(original.Id);
        originalAfter.Should().NotBeNull();
        originalAfter!.Id.Should().Be(original.Id);
        originalAfter.Quantity.Should().Be(original.Quantity);
        originalAfter.Direction.Should().Be(original.Direction);
        originalAfter.MovementType.Should().Be(original.MovementType);
        originalAfter.Reason.Should().Be("Order 123");
        originalAfter.CreatedAt.Should().Be(original.CreatedAt);
    }

    [Fact]
    public async Task CanReverseAsyncReturnsTrueForEligibleAndFalseOnceReversed()
    {
        var (service, movementRepo, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-7", "Salt", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-7", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Consumption, 2m, "kg", StockMovementSourceType.Order);
        await movementRepo.AppendAsync(original);

        var canBefore = await service.CanReverseAsync(original.Id);
        canBefore.Should().BeTrue();

        var result = await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Correction"));

        var canAfter = await service.CanReverseAsync(original.Id);
        canAfter.Should().BeFalse();

        var canReversalItself = await service.CanReverseAsync(result.ReversalMovement.Id);
        canReversalItself.Should().BeFalse();
    }

    [Fact]
    public async Task GetReversalForMovementReturnsExistingReversalOrNull()
    {
        var (service, movementRepo, itemRepo, locRepo, _, _) = CreateTestHarness();

        var item = StockItem.Create("SKU-8", "Pepper", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-8", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var original = StockMovement.Create(item.Id, loc.Id, StockMovementType.Consumption, 1m, "kg", StockMovementSourceType.Order);
        await movementRepo.AppendAsync(original);

        var revBefore = await service.GetReversalForMovementAsync(original.Id);
        revBefore.Should().BeNull();

        var result = await service.ReverseMovementAsync(new StockMovementReversalRequest(original.Id, "Correction"));

        var revAfter = await service.GetReversalForMovementAsync(original.Id);
        revAfter.Should().NotBeNull();
        revAfter!.Id.Should().Be(result.ReversalMovement.Id);
    }

    private static (
        StockMovementReversalService service,
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

        var service = new StockMovementReversalService(
            movementRepo, itemRepo, locRepo, projector, balanceRepo);

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

        public Task AppendAsync(StockMovement movement, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
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

        public Task<StockBalance> ApplyOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
            => ApplyOnHandDeltaAsync(stockItemId, stockLocationId, onHandDelta, ct);

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
