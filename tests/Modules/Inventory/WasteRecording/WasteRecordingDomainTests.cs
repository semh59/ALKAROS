using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Measurements;
using Npgsql;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.WasteRecording.Tests;

public sealed class WasteRecordingDomainTests
{
    private static (
        WasteRecordingService service,
        FakeWasteRecordRepository wasteRepo,
        FakeStockMovementRepository movementRepo,
        FakeStockItemRepository itemRepo,
        FakeStockLocationRepository locRepo,
        FakeStockBalanceRepository balanceRepo) CreateTestHarness()
    {
        var wasteRepo = new FakeWasteRecordRepository();
        var movementRepo = new FakeStockMovementRepository();
        var itemRepo = new FakeStockItemRepository();
        var locRepo = new FakeStockLocationRepository();
        var balanceRepo = new FakeStockBalanceRepository();
        var projector = new StockBalanceProjector(balanceRepo, movementRepo, locRepo);
        var unitConverter = new UnitConverter();
        var transactionRunner = new FakeInventoryTransactionRunner();

        var service = new WasteRecordingService(
            transactionRunner, wasteRepo, movementRepo, itemRepo, locRepo, balanceRepo, unitConverter);

        return (service, wasteRepo, movementRepo, itemRepo, locRepo, balanceRepo);
    }

    [Fact]
    public async Task RecordWasteWithValidRequestAppendsStockMovementAndDecrementsBalance()
    {
        var (service, wasteRepo, movementRepo, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-1", "Flour", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);

        var loc = StockLocation.Create("LOC-WST-1", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();
        var sourceRefId = Guid.NewGuid();

        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Production,
            Quantity: 2.5m,
            UnitCode: "kg",
            Reason: "Burnt batch during prep",
            RecordedBy: userId,
            SourceReferenceId: sourceRefId,
            IdempotencyKey: "idemp-001");

        var result = await service.RecordWasteAsync(request);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Record.Should().NotBeNull();
        result.Record.StockItemId.Should().Be(item.Id);
        result.Record.StockLocationId.Should().Be(loc.Id);
        result.Record.WasteSource.Should().Be(WasteSources.Production);
        result.Record.Quantity.Should().Be(2.5m);
        result.Record.NormalizedQuantity.Should().Be(2.5m);
        result.Record.WasteReason.Should().Be("Burnt batch during prep");
        result.Record.RecordedBy.Should().Be(userId);
        result.Record.SourceReferenceId.Should().Be(sourceRefId);
        result.Record.IdempotencyKey.Should().Be("idemp-001");

        result.Movement.Should().NotBeNull();
        result.Movement.MovementType.Should().Be(StockMovementType.Waste);
        result.Movement.Direction.Should().Be(MovementDirection.Out);
        result.Movement.Quantity.Should().Be(2.5m);
        result.Movement.UnitCode.Should().Be("kg");
        result.Movement.SourceType.Should().Be(StockMovementSourceType.WasteRecord);
        result.Movement.SourceReferenceId.Should().Be(result.Record.Id);

        var updatedBalance = await balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        updatedBalance!.OnHandQuantity.Should().Be(7.5m);
    }

    [Fact]
    public async Task RecordWasteWithCompatibleUnitConvertsQuantityCorrectly()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-2", "Sugar", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-2", "Bakery", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();

        // 500 grams = 0.5 kg
        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.PreparationDamage,
            Quantity: 500m,
            UnitCode: "g",
            Reason: "Dropped prep pan",
            RecordedBy: userId);

        var result = await service.RecordWasteAsync(request);

        result.Record.Quantity.Should().Be(500m);
        result.Record.UnitCode.Should().Be("g");
        result.Record.NormalizedQuantity.Should().Be(0.5m);
        result.Record.TrackingUnitCode.Should().Be("kg");
        result.Movement.Quantity.Should().Be(0.5m);

        var updatedBalance = await balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        updatedBalance!.OnHandQuantity.Should().Be(9.5m);
    }

    [Fact]
    public async Task RecordWasteWithIncompatibleUnitThrowsIncompatibleWasteUnitException()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-3", "Salt", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-3", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();

        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Manual,
            Quantity: 500m,
            UnitCode: "ml",
            Reason: "Spill",
            RecordedBy: userId);

        var act = () => service.RecordWasteAsync(request);
        await act.Should().ThrowAsync<IncompatibleWasteUnitException>();
    }

    [Fact]
    public async Task RecordWasteWithNonPositiveQuantityThrowsInvalidWasteQuantityException()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-4", "Meat", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-4", "Butcher", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();

        var requestZero = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Manual,
            Quantity: 0m,
            UnitCode: "kg",
            Reason: "Reason",
            RecordedBy: userId);

        var actZero = () => service.RecordWasteAsync(requestZero);
        await actZero.Should().ThrowAsync<InvalidWasteQuantityException>();

        var requestNegative = requestZero with { Quantity = -5m };
        var actNegative = () => service.RecordWasteAsync(requestNegative);
        await actNegative.Should().ThrowAsync<InvalidWasteQuantityException>();
    }

    [Fact]
    public async Task RecordWasteWithEmptyReasonThrowsInvalidWasteReasonException()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-5", "Rice", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-5", "Pantry", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();

        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Manual,
            Quantity: 1m,
            UnitCode: "kg",
            Reason: "   ",
            RecordedBy: userId);

        var act = () => service.RecordWasteAsync(request);
        await act.Should().ThrowAsync<InvalidWasteReasonException>();
    }

    [Fact]
    public async Task RecordWasteWithEmptyRecordedByThrowsUnauthorizedWasteRecorderException()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-6", "Beans", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-6", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Manual,
            Quantity: 1m,
            UnitCode: "kg",
            Reason: "Expired ingredient",
            RecordedBy: Guid.Empty);

        var act = () => service.RecordWasteAsync(request);
        await act.Should().ThrowAsync<UnauthorizedWasteRecorderException>();
    }

    [Fact]
    public async Task RecordWasteWithInsufficientStockThrowsInsufficientStockForWasteException()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-7", "Cheese", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-7", "Fridge", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 2m);

        var userId = Guid.NewGuid();

        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Spoilage,
            Quantity: 5m,
            UnitCode: "kg",
            Reason: "Moldy cheese",
            RecordedBy: userId);

        var act = () => service.RecordWasteAsync(request);
        await act.Should().ThrowAsync<InsufficientStockForWasteException>();

        var balance = await balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balance!.OnHandQuantity.Should().Be(2m);
    }

    [Fact]
    public async Task RecordWasteWithDuplicateIdempotencyKeyReturnsExistingWithoutDuplicateLedgerEntry()
    {
        var (service, _, movementRepo, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-8", "Beef", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-8", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();

        var request = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.PortionReservation,
            Quantity: 1m,
            UnitCode: "kg",
            Reason: "Order cancelled after preparation started",
            RecordedBy: userId,
            SourceReferenceId: Guid.NewGuid(),
            IdempotencyKey: "reservation-waste-orderitem-999");

        var firstResult = await service.RecordWasteAsync(request);
        firstResult.IsIdempotentReplay.Should().BeFalse();

        var secondResult = await service.RecordWasteAsync(request);
        secondResult.IsIdempotentReplay.Should().BeTrue();
        secondResult.Record.Id.Should().Be(firstResult.Record.Id);
        secondResult.Movement.Id.Should().Be(firstResult.Movement.Id);

        movementRepo.Movements.Count.Should().Be(1);

        var balance = await balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balance!.OnHandQuantity.Should().Be(9m);
    }

    [Fact]
    public async Task GetWasteRecordsBySourceReturnsAllLinkedRecords()
    {
        var (service, _, _, itemRepo, locRepo, balanceRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-WST-9", "Chicken", StockItemType.RawMaterial, "kg");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-WST-9", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);
        await balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 10m);

        var userId = Guid.NewGuid();
        var productionOrderId = Guid.NewGuid();

        await service.RecordWasteAsync(new RecordWasteRequest(
            item.Id, loc.Id, WasteSources.Production, 1m, "kg", "Burned 1", userId, SourceReferenceId: productionOrderId));
        await service.RecordWasteAsync(new RecordWasteRequest(
            item.Id, loc.Id, WasteSources.Production, 2m, "kg", "Burned 2", userId, SourceReferenceId: productionOrderId));

        var records = await service.GetWasteRecordsBySourceAsync(WasteSources.Production, productionOrderId);
        records.Count.Should().Be(2);
        records.Sum(r => r.Quantity).Should().Be(3m);
    }

    public sealed class FakeWasteRecordRepository : IWasteRecordRepository
    {
        private readonly List<WasteRecord> _records = new();

        public Task InsertAsync(WasteRecord record, CancellationToken ct = default)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task InsertAsync(WasteRecord record, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
            => InsertAsync(record, ct);

        public Task<WasteRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_records.FirstOrDefault(r => r.Id == id));

        public Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
            => Task.FromResult(_records.FirstOrDefault(r => string.Equals(r.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<WasteRecord>> GetBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken ct = default)
        {
            var list = _records.Where(r => string.Equals(r.WasteSource, wasteSource, StringComparison.OrdinalIgnoreCase) && r.SourceReferenceId == sourceReferenceId).ToList();
            return Task.FromResult<IReadOnlyList<WasteRecord>>(list);
        }
    }

    public sealed class FakeStockMovementRepository : IStockMovementRepository
    {
        private readonly List<StockMovement> _movements = new();

        public IReadOnlyList<StockMovement> Movements => _movements;

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

    public sealed class FakeStockItemRepository : IStockItemRepository
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

    public sealed class FakeStockLocationRepository : IStockLocationRepository
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

    public sealed class FakeStockBalanceRepository : IStockBalanceRepository
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

        public Task<StockBalance?> TryApplyGuardedOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
        {
            var current = _balances.TryGetValue((stockItemId, stockLocationId), out var existing) ? existing.OnHandQuantity : 0m;
            if (current + onHandDelta < 0m)
                return Task.FromResult<StockBalance?>(null);

            return ApplyOnHandDeltaAsync(stockItemId, stockLocationId, onHandDelta, ct)
                .ContinueWith(t => (StockBalance?)t.Result, ct);
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

    /// <summary>
    /// Runs the operation directly against the fakes, no real connection/
    /// transaction needed — the fakes' own (connection, transaction)
    /// overloads already ignore those parameters and delegate to their
    /// plain in-memory implementation.
    /// </summary>
    private sealed class FakeInventoryTransactionRunner : IInventoryTransactionRunner
    {
        public Task<T> RunAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operation, CancellationToken ct = default)
            => operation(null!, null!);
    }
}
