using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Recipes.Units;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects.Tests;

public sealed class FakeKitchenItemStateProvider : IKitchenItemStateProvider
{
    private readonly Dictionary<Guid, KitchenItemPreparationStatus> _statuses = new();

    public void SetStatus(Guid orderItemId, KitchenItemPreparationStatus status) =>
        _statuses[orderItemId] = status;

    public Task<KitchenItemPreparationStatus> GetItemPreparationStatusAsync(Guid orderItemId, CancellationToken ct = default) =>
        Task.FromResult(_statuses.GetValueOrDefault(orderItemId, KitchenItemPreparationStatus.NotStarted));
}

public sealed class FakePortionReservationRepository : IPortionReservationRepository
{
    private readonly Dictionary<Guid, PortionReservation> _reservations = new();
    private readonly Dictionary<string, PortionReservation> _idempIndex = new();

    public void Add(PortionReservation reservation)
    {
        _reservations[reservation.Id] = Clone(reservation);
        if (!string.IsNullOrWhiteSpace(reservation.IdempotencyKey))
            _idempIndex[reservation.IdempotencyKey] = Clone(reservation);
    }

    public Task InsertAsync(PortionReservation reservation, CancellationToken cancellationToken = default)
    {
        Add(reservation);
        return Task.CompletedTask;
    }

    public Task<PortionReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_reservations.TryGetValue(id, out var r) ? Clone(r) : null);

    public Task<PortionReservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_idempIndex.TryGetValue(idempotencyKey, out var r) ? Clone(r) : null);

    public Task<IReadOnlyList<PortionReservation>> GetByOrderItemIdAsync(Guid orderItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PortionReservation>>(_reservations.Values.Where(r => r.OrderItemId == orderItemId).Select(Clone).ToList());

    public Task<IReadOnlyList<PortionReservation>> GetActiveByStockItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PortionReservation>>(_reservations.Values.Where(r => r.StockItemId == stockItemId && r.StockLocationId == stockLocationId && r.Status == PortionReservationStatus.Reserved).Select(Clone).ToList());

    public Task<bool> UpdateStatusOptimisticAsync(PortionReservation reservation, int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!_reservations.TryGetValue(reservation.Id, out var existing))
            return Task.FromResult(false);

        if (existing.Version != expectedVersion)
            return Task.FromResult(false);

        _reservations[reservation.Id] = Clone(reservation);
        return Task.FromResult(true);
    }

    private static PortionReservation Clone(PortionReservation r) =>
        new(r.Id, r.OrderId, r.OrderItemId, r.StockItemId, r.StockLocationId, r.Quantity, r.UnitCode,
            r.Status, r.Version, r.ReservedAt, r.CreatedBy, r.IdempotencyKey, r.TransitionedAt,
            r.TransitionReason, r.TransitionedBy, r.MetadataJson);
}

public sealed class FakeStockLocationRepository : IStockLocationRepository
{
    private readonly Dictionary<Guid, StockLocation> _locations = new();
    public void Add(StockLocation loc) => _locations[loc.Id] = loc;
    public Task AddAsync(StockLocation location, CancellationToken ct = default) { Add(location); return Task.CompletedTask; }
    public Task<StockLocation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_locations.GetValueOrDefault(id));
    public Task<StockLocation?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(_locations.Values.FirstOrDefault(l => l.Code == code));
    public Task<IReadOnlyList<StockLocation>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StockLocation>>(_locations.Values.ToList());
    public Task UpdateAsync(StockLocation location, CancellationToken ct = default) { Add(location); return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _locations.Remove(id); return Task.CompletedTask; }
}

public sealed class FakeStockItemRepository : IStockItemRepository
{
    private readonly Dictionary<Guid, StockItem> _items = new();
    public void Add(StockItem item) => _items[item.Id] = item;
    public Task AddAsync(StockItem item, CancellationToken ct = default) { Add(item); return Task.CompletedTask; }
    public Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_items.GetValueOrDefault(id));
    public Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(_items.Values.FirstOrDefault(i => i.Code == code));
    public Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.ToList());
    public Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.Where(i => i.ItemType == itemType).ToList());
    public Task UpdateAsync(StockItem item, CancellationToken ct = default) { Add(item); return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _items.Remove(id); return Task.CompletedTask; }
}

public sealed class FakeWasteRecordRepository : IWasteRecordRepository
{
    private readonly Dictionary<Guid, WasteRecord> _records = new();
    public Task InsertAsync(WasteRecord record, CancellationToken ct = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }
    public Task<WasteRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_records.GetValueOrDefault(id));
    public Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        Task.FromResult(_records.Values.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey));
    public Task<IReadOnlyList<WasteRecord>> GetBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<WasteRecord>>(_records.Values.Where(r => r.WasteSource == wasteSource && r.SourceReferenceId == sourceReferenceId).ToList());
}

public sealed class FakeStockMovementRepository : IStockMovementRepository
{
    private readonly List<StockMovement> _movements = new();
    public Task AppendAsync(StockMovement movement, CancellationToken ct = default)
    {
        _movements.Add(movement);
        return Task.CompletedTask;
    }
    public Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_movements.FirstOrDefault(m => m.Id == id));
    public Task<IReadOnlyList<StockMovement>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(_movements.Where(m => m.StockItemId == stockItemId).ToList());
    public Task<IReadOnlyList<StockMovement>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(_movements.Where(m => m.StockLocationId == stockLocationId).ToList());
    public Task<IReadOnlyList<StockMovement>> GetBySourceAsync(string sourceType, Guid sourceReferenceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(_movements.Where(m => m.SourceType == sourceType && m.SourceReferenceId == sourceReferenceId).ToList());
    public Task<bool> HasReversalAsync(Guid stockMovementId, CancellationToken ct = default) =>
        Task.FromResult(_movements.Any(m => m.MovementType == StockMovementType.Reversal && m.SourceReferenceId == stockMovementId));
}

public sealed class FakeStockBalanceRepository : IStockBalanceRepository
{
    private readonly Dictionary<(Guid Item, Guid Loc), StockBalance> _balances = new();

    public void Seed(StockBalance b) => _balances[(b.StockItemId, b.StockLocationId)] = b;
    public Task<StockBalance?> GetByItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default) =>
        Task.FromResult(_balances.GetValueOrDefault((stockItemId, stockLocationId)));
    public Task<IReadOnlyList<StockBalance>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.Where(b => b.StockItemId == stockItemId).ToList());
    public Task<IReadOnlyList<StockBalance>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.Where(b => b.StockLocationId == stockLocationId).ToList());
    public Task<IReadOnlyList<StockBalance>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.ToList());
    public Task<StockBalance> ApplyOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, CancellationToken ct = default)
    {
        var key = (stockItemId, stockLocationId);
        if (!_balances.TryGetValue(key, out var b))
            b = StockBalance.Create(stockItemId, stockLocationId);
        b.ApplyOnHandDelta(onHandDelta);
        _balances[key] = b;
        return Task.FromResult(b);
    }
    public Task SetExactBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal onHandQuantity, CancellationToken ct = default)
    {
        var key = (stockItemId, stockLocationId);
        if (!_balances.TryGetValue(key, out var b))
            b = StockBalance.Create(stockItemId, stockLocationId);
        b.SetOnHand(onHandQuantity);
        _balances[key] = b;
        return Task.CompletedTask;
    }
    public Task ResetAllBalancesAsync(CancellationToken ct = default)
    {
        _balances.Clear();
        return Task.CompletedTask;
    }
}

public sealed class FakeReservationBalanceRepository : IReservationBalanceRepository
{
    private readonly FakeStockBalanceRepository _stockBalRepo;
    private readonly HashSet<(Guid ResId, string EventType)> _applied = new();

    public FakeReservationBalanceRepository(FakeStockBalanceRepository stockBalRepo)
    {
        _stockBalRepo = stockBalRepo;
    }

    public Task<StockBalance?> GetBalanceAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default) =>
        _stockBalRepo.GetByItemAndLocationAsync(stockItemId, stockLocationId, ct);

    public Task<IReadOnlyList<StockBalance>> GetAllBalancesAsync(CancellationToken ct = default) =>
        _stockBalRepo.GetAllAsync(ct);

    public async Task<ApplyReservationResult> ApplyReservationCreatedAtomicAsync(PortionReservation reservation, CancellationToken ct = default)
    {
        var key = (reservation.Id, ReservationAppliedEventType.Reserved);
        if (_applied.Contains(key))
        {
            var existing = await _stockBalRepo.GetByItemAndLocationAsync(reservation.StockItemId, reservation.StockLocationId, ct);
            return new ApplyReservationResult(existing!, true);
        }

        _applied.Add(key);
        var bal = await _stockBalRepo.GetByItemAndLocationAsync(reservation.StockItemId, reservation.StockLocationId, ct);
        var updated = new StockBalance(bal!.Id, bal.StockItemId, bal.StockLocationId, bal.OnHandQuantity,
            bal.ReservedQuantity + reservation.Quantity, bal.OnHandQuantity - (bal.ReservedQuantity + reservation.Quantity));
        _stockBalRepo.Seed(updated);
        return new ApplyReservationResult(updated, false);
    }

    public async Task<ApplyReservationResult> ApplyReservationTerminalAtomicAsync(PortionReservation reservation, PortionReservationStatus terminalStatus, CancellationToken ct = default)
    {
        var key = (reservation.Id, ReservationAppliedEventType.Terminal);
        if (_applied.Contains(key))
        {
            var existing = await _stockBalRepo.GetByItemAndLocationAsync(reservation.StockItemId, reservation.StockLocationId, ct);
            return new ApplyReservationResult(existing!, true);
        }

        _applied.Add(key);
        var bal = await _stockBalRepo.GetByItemAndLocationAsync(reservation.StockItemId, reservation.StockLocationId, ct);
        var newRes = Math.Max(0m, bal!.ReservedQuantity - reservation.Quantity);
        var updated = new StockBalance(bal.Id, bal.StockItemId, bal.StockLocationId, bal.OnHandQuantity,
            newRes, bal.OnHandQuantity - newRes);
        _stockBalRepo.Seed(updated);
        return new ApplyReservationResult(updated, false);
    }

    public Task SetExactReservedBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal reservedQuantity, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>> AggregateActiveReservationsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>>(new Dictionary<(Guid, Guid), decimal>());
    public Task ResetAppliedEventsAsync(CancellationToken ct = default) { _applied.Clear(); return Task.CompletedTask; }
    public Task RecordAppliedEventsForRebuildAsync(IReadOnlyList<ReservationAppliedEvent> events, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class PortionReservationCancellationEffectsDomainTests
{
    private readonly FakePortionReservationRepository _reservationRepo;
    private readonly FakeStockLocationRepository _locationRepo;
    private readonly FakeStockItemRepository _itemRepo;
    private readonly FakeStockBalanceRepository _stockBalanceRepo;
    private readonly FakeReservationBalanceRepository _resBalanceRepo;
    private readonly FakeWasteRecordRepository _wasteRepo;
    private readonly FakeKitchenItemStateProvider _kitchenProvider;
    private readonly PortionReservationLifecycleService _lifecycleService;
    private readonly ReservationBalanceProjector _balanceProjector;
    private readonly WasteRecordingService _wasteService;
    private readonly PortionCancellationDecisionService _decisionService;

    private readonly Guid _stockItemId = Guid.NewGuid();
    private readonly Guid _stockLocationId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();

    public PortionReservationCancellationEffectsDomainTests()
    {
        _reservationRepo = new FakePortionReservationRepository();
        _locationRepo = new FakeStockLocationRepository();
        _itemRepo = new FakeStockItemRepository();
        _stockBalanceRepo = new FakeStockBalanceRepository();
        _resBalanceRepo = new FakeReservationBalanceRepository(_stockBalanceRepo);
        _wasteRepo = new FakeWasteRecordRepository();
        _kitchenProvider = new FakeKitchenItemStateProvider();

        var unitConverter = new UnitConverter();
        _lifecycleService = new PortionReservationLifecycleService(_reservationRepo, _itemRepo, _locationRepo);
        _balanceProjector = new ReservationBalanceProjector(_resBalanceRepo);

        var movementRepo = new FakeStockMovementRepository();
        var stockProjector = new StockBalanceProjector(_stockBalanceRepo, movementRepo, _locationRepo);
        _wasteService = new WasteRecordingService(_wasteRepo, movementRepo, _itemRepo, _locationRepo, _stockBalanceRepo, stockProjector, unitConverter);

        _decisionService = new PortionCancellationDecisionService(
            _reservationRepo, _lifecycleService, _balanceProjector, _wasteService, _kitchenProvider);

        _locationRepo.Add(new StockLocation(_stockLocationId, "KITCHEN-1", "Main Kitchen", StockLocationType.Kitchen));
        _itemRepo.Add(new StockItem(_stockItemId, "SKU-SALMON", "Salmon Portion", StockItemType.RawMaterial, "portion"));

        _stockBalanceRepo.Seed(new StockBalance(Guid.NewGuid(), _stockItemId, _stockLocationId, 10m, 2m, 8m));
    }

    [Fact]
    public async Task ProcessCancellationWhenKitchenStatusNotStartedReleasesReservationAndRestoresAvailable()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var reservation = PortionReservation.Create(orderId, orderItemId, _stockItemId, _stockLocationId, 2m, "portion", _staffId);
        _reservationRepo.Add(reservation);

        _kitchenProvider.SetStatus(orderItemId, KitchenItemPreparationStatus.NotStarted);

        var cmd = new ProcessCancellationCommand(reservation.Id, orderItemId, _staffId, "Customer changed mind");
        var result = await _decisionService.ProcessCancellationAsync(cmd);

        result.Action.Should().Be(CancellationAction.Release);
        result.IsIdempotentReplay.Should().BeFalse();
        result.Reservation.Status.Should().Be(PortionReservationStatus.Released);
        result.WasteRecord.Should().BeNull();

        var bal = await _stockBalanceRepo.GetByItemAndLocationAsync(_stockItemId, _stockLocationId);
        bal!.OnHandQuantity.Should().Be(10m);
        bal.ReservedQuantity.Should().Be(0m);
        bal.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task ProcessCancellationWhenKitchenStatusInProgressWastesReservationAndDoesNotRestoreAvailable()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var reservation = PortionReservation.Create(orderId, orderItemId, _stockItemId, _stockLocationId, 2m, "portion", _staffId);
        _reservationRepo.Add(reservation);

        _kitchenProvider.SetStatus(orderItemId, KitchenItemPreparationStatus.InProgress);

        var cmd = new ProcessCancellationCommand(reservation.Id, orderItemId, _staffId, "Cancelled while cooking");
        var result = await _decisionService.ProcessCancellationAsync(cmd);

        result.Action.Should().Be(CancellationAction.Waste);
        result.IsIdempotentReplay.Should().BeFalse();
        result.Reservation.Status.Should().Be(PortionReservationStatus.Waste);
        result.WasteRecord.Should().NotBeNull();
        result.WasteRecord!.Quantity.Should().Be(2m);

        var bal = await _stockBalanceRepo.GetByItemAndLocationAsync(_stockItemId, _stockLocationId);
        bal!.OnHandQuantity.Should().Be(8m);
        bal.ReservedQuantity.Should().Be(0m);
        bal.AvailableQuantity.Should().Be(8m);
    }

    [Fact]
    public async Task ProcessCancellationWhenKitchenStatusCompletedWastesReservationAndDoesNotRestoreAvailable()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var reservation = PortionReservation.Create(orderId, orderItemId, _stockItemId, _stockLocationId, 2m, "portion", _staffId);
        _reservationRepo.Add(reservation);

        _kitchenProvider.SetStatus(orderItemId, KitchenItemPreparationStatus.Completed);

        var cmd = new ProcessCancellationCommand(reservation.Id, orderItemId, _staffId, "Customer left after food was ready");
        var result = await _decisionService.ProcessCancellationAsync(cmd);

        result.Action.Should().Be(CancellationAction.Waste);
        result.IsIdempotentReplay.Should().BeFalse();
        result.Reservation.Status.Should().Be(PortionReservationStatus.Waste);
        result.WasteRecord.Should().NotBeNull();

        var bal = await _stockBalanceRepo.GetByItemAndLocationAsync(_stockItemId, _stockLocationId);
        bal!.OnHandQuantity.Should().Be(8m);
        bal.ReservedQuantity.Should().Be(0m);
        bal.AvailableQuantity.Should().Be(8m);
    }

    [Fact]
    public async Task ProcessCancellationDuplicateCallReturnsIdempotentReplayWithoutDuplicateSideEffects()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var reservation = PortionReservation.Create(orderId, orderItemId, _stockItemId, _stockLocationId, 2m, "portion", _staffId);
        _reservationRepo.Add(reservation);

        _kitchenProvider.SetStatus(orderItemId, KitchenItemPreparationStatus.NotStarted);

        var cmd = new ProcessCancellationCommand(reservation.Id, orderItemId, _staffId, "Cancelled pre-prep");
        var first = await _decisionService.ProcessCancellationAsync(cmd);
        var second = await _decisionService.ProcessCancellationAsync(cmd);

        first.IsIdempotentReplay.Should().BeFalse();
        second.IsIdempotentReplay.Should().BeTrue();
        second.Action.Should().Be(CancellationAction.Release);

        var bal = await _stockBalanceRepo.GetByItemAndLocationAsync(_stockItemId, _stockLocationId);
        bal!.OnHandQuantity.Should().Be(10m);
        bal.ReservedQuantity.Should().Be(0m);
        bal.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task ProcessCancellationForConsumedReservationThrowsInvalidCancellationCommandException()
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var reservation = PortionReservation.Create(orderId, orderItemId, _stockItemId, _stockLocationId, 2m, "portion", _staffId);
        reservation.TransitionTo(PortionReservationStatus.Consumed, _staffId, "Served");
        _reservationRepo.Add(reservation);

        var cmd = new ProcessCancellationCommand(reservation.Id, orderItemId, _staffId);
        var act = () => _decisionService.ProcessCancellationAsync(cmd);
        await act.Should().ThrowAsync<InvalidCancellationCommandException>();
    }

    [Fact]
    public async Task ProcessCancellationWithEmptyReservationIdThrowsInvalidCancellationCommandException()
    {
        var cmd = new ProcessCancellationCommand(Guid.Empty, Guid.NewGuid(), _staffId);
        var act = () => _decisionService.ProcessCancellationAsync(cmd);
        await act.Should().ThrowAsync<InvalidCancellationCommandException>();
    }
}
