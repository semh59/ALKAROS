using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using Npgsql;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.BalanceProjection.Tests;

public sealed class StockBalanceDomainTests
{
    [Fact]
    public void CreateStockBalanceInitializesPropertiesCorrectly()
    {
        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        var balance = StockBalance.Create(itemId, locId, initialOnHand: 100m);

        balance.Id.Should().NotBeEmpty();
        balance.StockItemId.Should().Be(itemId);
        balance.StockLocationId.Should().Be(locId);
        balance.OnHandQuantity.Should().Be(100m);
        balance.ReservedQuantity.Should().Be(0m);
        balance.AvailableQuantity.Should().Be(100m);
        balance.RowVersion.Should().Be(1);
    }

    [Fact]
    public void ApplyOnHandDeltaUpdatesOnHandAndAvailable()
    {
        var balance = StockBalance.Create(Guid.NewGuid(), Guid.NewGuid(), initialOnHand: 50m);

        balance.ApplyOnHandDelta(25m);
        balance.OnHandQuantity.Should().Be(75m);
        balance.AvailableQuantity.Should().Be(75m);

        balance.ApplyOnHandDelta(-30m);
        balance.OnHandQuantity.Should().Be(45m);
        balance.AvailableQuantity.Should().Be(45m);
    }

    [Fact]
    public void SetOnHandUpdatesOnHandAndAvailablePreservingReserved()
    {
        var balance = new StockBalance(
            id: Guid.NewGuid(),
            stockItemId: Guid.NewGuid(),
            stockLocationId: Guid.NewGuid(),
            onHandQuantity: 50m,
            reservedQuantity: 10m,
            availableQuantity: 40m);

        balance.SetOnHand(80m);
        balance.OnHandQuantity.Should().Be(80m);
        balance.ReservedQuantity.Should().Be(10m);
        balance.AvailableQuantity.Should().Be(70m);
    }

    [Fact]
    public void NullPolicyRejectsEmptyGuids()
    {
        var actEmptyItem = () => new StockBalance(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 0m);
        actEmptyItem.Should().Throw<ArgumentException>()
            .WithMessage("*StockItemId*");

        var actEmptyLoc = () => new StockBalance(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 0m);
        actEmptyLoc.Should().Throw<ArgumentException>()
            .WithMessage("*StockLocationId*");
    }

    [Fact]
    public async Task StockBalanceProjectorAppliesMovementOnHandDelta()
    {
        var balanceRepo = new FakeStockBalanceRepository();
        var movementRepo = new FakeStockMovementRepository();
        var locRepo = new FakeStockLocationRepository();
        var projector = new StockBalanceProjector(balanceRepo, movementRepo, locRepo);

        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        // 1. Receipt (+50)
        var receipt = StockMovement.Create(
            itemId, locId, StockMovementType.PurchaseReceipt, 50m, "kg", StockMovementSourceType.PurchaseOrder);
        await projector.ApplyMovementAsync(receipt);

        var bal1 = await balanceRepo.GetByItemAndLocationAsync(itemId, locId);
        bal1.Should().NotBeNull();
        bal1!.OnHandQuantity.Should().Be(50m);

        // 2. Consumption (-15)
        var consumption = StockMovement.Create(
            itemId, locId, StockMovementType.Consumption, 15m, "kg", StockMovementSourceType.Order);
        await projector.ApplyMovementAsync(consumption);

        var bal2 = await balanceRepo.GetByItemAndLocationAsync(itemId, locId);
        bal2!.OnHandQuantity.Should().Be(35m);

        // 3. Reservation (OnHand delta = 0)
        var reservation = StockMovement.Create(
            itemId, locId, StockMovementType.Reservation, 10m, "kg", StockMovementSourceType.DailyMenu);
        await projector.ApplyMovementAsync(reservation);

        var bal3 = await balanceRepo.GetByItemAndLocationAsync(itemId, locId);
        bal3!.OnHandQuantity.Should().Be(35m);
    }

    [Fact]
    public async Task StockBalanceProjectorReplaysLedgerDeterministically()
    {
        var balanceRepo = new FakeStockBalanceRepository();
        var movementRepo = new FakeStockMovementRepository();
        var locRepo = new FakeStockLocationRepository();
        var projector = new StockBalanceProjector(balanceRepo, movementRepo, locRepo);

        var itemId = Guid.NewGuid();
        var locId = Guid.NewGuid();

        // Add 4 movements to ledger
        var m1 = StockMovement.Create(itemId, locId, StockMovementType.PurchaseReceipt, 100m, "kg", StockMovementSourceType.PurchaseOrder);
        var m2 = StockMovement.Create(itemId, locId, StockMovementType.Consumption, 30m, "kg", StockMovementSourceType.Order);
        var m3 = StockMovement.Create(itemId, locId, StockMovementType.Waste, 5m, "kg", StockMovementSourceType.WasteRecord);
        var m4 = StockMovement.CreateReversal(m2); // Reversal adds +30 back

        await movementRepo.AppendAsync(m1);
        await movementRepo.AppendAsync(m2);
        await movementRepo.AppendAsync(m3);
        await movementRepo.AppendAsync(m4);

        var replayedBalance = await projector.ReplayBalanceForItemAndLocationAsync(itemId, locId);
        // 100 - 30 - 5 + 30 = 95
        replayedBalance.Should().Be(95m);
    }

    [Fact]
    public async Task StockBalanceProjectorRebuildAllBalancesClearsAndReplaysAllLocations()
    {
        var balanceRepo = new FakeStockBalanceRepository();
        var movementRepo = new FakeStockMovementRepository();
        var locRepo = new FakeStockLocationRepository();
        var projector = new StockBalanceProjector(balanceRepo, movementRepo, locRepo);

        var loc1 = StockLocation.Create("LOC-1", "Main Store", StockLocationType.Warehouse);
        var loc2 = StockLocation.Create("LOC-2", "Kitchen Store", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc1);
        await locRepo.AddAsync(loc2);

        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();

        // Put stale/drifted balance in balanceRepo
        await balanceRepo.SetExactBalanceAsync(itemA, loc1.Id, 9999m);

        // Movements:
        // loc1, itemA: +100 - 20 = 80
        await movementRepo.AppendAsync(StockMovement.Create(itemA, loc1.Id, StockMovementType.PurchaseReceipt, 100m, "kg", StockMovementSourceType.PurchaseOrder));
        await movementRepo.AppendAsync(StockMovement.Create(itemA, loc1.Id, StockMovementType.Consumption, 20m, "kg", StockMovementSourceType.Order));

        // loc2, itemB: +50
        await movementRepo.AppendAsync(StockMovement.Create(itemB, loc2.Id, StockMovementType.PurchaseReceipt, 50m, "kg", StockMovementSourceType.PurchaseOrder));

        var report = await projector.RebuildAllBalancesAsync();
        report.TotalMovementsProcessed.Should().Be(3);
        report.TotalBalancesUpdated.Should().Be(2);

        var balA = await balanceRepo.GetByItemAndLocationAsync(itemA, loc1.Id);
        balA.Should().NotBeNull();
        balA!.OnHandQuantity.Should().Be(80m);

        var balB = await balanceRepo.GetByItemAndLocationAsync(itemB, loc2.Id);
        balB.Should().NotBeNull();
        balB!.OnHandQuantity.Should().Be(50m);
    }

    private sealed class FakeStockBalanceRepository : IStockBalanceRepository
    {
        private readonly Dictionary<(Guid, Guid), StockBalance> _balances = new();

        public Task<StockBalance?> GetByItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default)
        {
            _balances.TryGetValue((stockItemId, stockLocationId), out var bal);
            return Task.FromResult(bal);
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
