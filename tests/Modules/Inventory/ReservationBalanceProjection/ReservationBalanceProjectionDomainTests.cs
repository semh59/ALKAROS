using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.ReservationBalanceProjection.Tests;

public sealed class FakeReservationBalanceRepository : IReservationBalanceRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<(Guid Item, Guid Location), StockBalance> _balances = new();
    private readonly HashSet<(Guid ResId, string EventType)> _appliedEvents = new();
    private readonly List<PortionReservation> _reservations = new();

    public void AddReservation(PortionReservation reservation)
    {
        lock (_lock)
        {
            _reservations.RemoveAll(r => r.Id == reservation.Id);
            _reservations.Add(reservation);
        }
    }

    public void SeedBalance(StockBalance balance)
    {
        lock (_lock)
        {
            _balances[(balance.StockItemId, balance.StockLocationId)] = balance;
        }
    }

    public Task<StockBalance?> GetBalanceAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _balances.TryGetValue((stockItemId, stockLocationId), out var balance);
            return Task.FromResult(balance);
        }
    }

    public Task<IReadOnlyList<StockBalance>> GetAllBalancesAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<StockBalance>>(_balances.Values.ToList());
        }
    }

    public Task<ApplyReservationResult> ApplyReservationCreatedAtomicAsync(PortionReservation reservation, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var key = (reservation.StockItemId, reservation.StockLocationId);
            var eventKey = (reservation.Id, ReservationAppliedEventType.Reserved);

            if (_appliedEvents.Contains(eventKey))
            {
                _balances.TryGetValue(key, out var cur);
                return Task.FromResult(new ApplyReservationResult(cur ?? StockBalance.Create(reservation.StockItemId, reservation.StockLocationId), true));
            }

            _appliedEvents.Add(eventKey);

            if (!_balances.TryGetValue(key, out var existing))
            {
                existing = StockBalance.Create(reservation.StockItemId, reservation.StockLocationId);
            }

            var newReserved = existing.ReservedQuantity + reservation.Quantity;
            var newAvailable = existing.OnHandQuantity - newReserved;
            var updated = new StockBalance(
                existing.Id,
                existing.StockItemId,
                existing.StockLocationId,
                existing.OnHandQuantity,
                newReserved,
                newAvailable,
                DateTimeOffset.UtcNow,
                existing.RowVersion + 1);

            _balances[key] = updated;
            return Task.FromResult(new ApplyReservationResult(updated, false));
        }
    }

    public Task<ApplyReservationResult> ApplyReservationTerminalAtomicAsync(PortionReservation reservation, PortionReservationStatus terminalStatus, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var key = (reservation.StockItemId, reservation.StockLocationId);
            var eventKey = (reservation.Id, ReservationAppliedEventType.Terminal);

            if (_appliedEvents.Contains(eventKey))
            {
                _balances.TryGetValue(key, out var cur);
                return Task.FromResult(new ApplyReservationResult(cur ?? StockBalance.Create(reservation.StockItemId, reservation.StockLocationId), true));
            }

            _appliedEvents.Add(eventKey);

            if (!_balances.TryGetValue(key, out var existing))
            {
                existing = StockBalance.Create(reservation.StockItemId, reservation.StockLocationId);
            }

            var newReserved = Math.Max(0m, existing.ReservedQuantity - reservation.Quantity);
            var newAvailable = existing.OnHandQuantity - newReserved;
            var updated = new StockBalance(
                existing.Id,
                existing.StockItemId,
                existing.StockLocationId,
                existing.OnHandQuantity,
                newReserved,
                newAvailable,
                DateTimeOffset.UtcNow,
                existing.RowVersion + 1);

            _balances[key] = updated;
            return Task.FromResult(new ApplyReservationResult(updated, false));
        }
    }

    public Task SetExactReservedBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal reservedQuantity, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var key = (stockItemId, stockLocationId);
            if (!_balances.TryGetValue(key, out var existing))
            {
                existing = StockBalance.Create(stockItemId, stockLocationId);
            }

            var updated = new StockBalance(
                existing.Id,
                existing.StockItemId,
                existing.StockLocationId,
                existing.OnHandQuantity,
                reservedQuantity,
                existing.OnHandQuantity - reservedQuantity,
                DateTimeOffset.UtcNow,
                existing.RowVersion + 1);

            _balances[key] = updated;
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>> AggregateActiveReservationsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            var dict = _reservations
                .Where(r => r.Status == PortionReservationStatus.Reserved)
                .GroupBy(r => (r.StockItemId, r.StockLocationId))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            return Task.FromResult<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>>(dict);
        }
    }

    public Task ResetAppliedEventsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _appliedEvents.Clear();
            return Task.CompletedTask;
        }
    }

    public Task RecordAppliedEventsForRebuildAsync(IReadOnlyList<ReservationAppliedEvent> events, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var ev in events)
            {
                _appliedEvents.Add((ev.ReservationId, ev.EventType));
            }
            return Task.CompletedTask;
        }
    }
}

public sealed class ReservationBalanceProjectionDomainTests
{
    private readonly FakeReservationBalanceRepository _repo;
    private readonly ReservationBalanceProjector _projector;
    private readonly Guid _stockItemId = Guid.NewGuid();
    private readonly Guid _stockLocationId = Guid.NewGuid();

    public ReservationBalanceProjectionDomainTests()
    {
        _repo = new FakeReservationBalanceRepository();
        _projector = new ReservationBalanceProjector(_repo);

        // Seed balance with 10 on-hand, 0 reserved, 10 available
        _repo.SeedBalance(new StockBalance(
            Guid.NewGuid(), _stockItemId, _stockLocationId, onHandQuantity: 10m, reservedQuantity: 0m, availableQuantity: 10m));
    }

    [Fact]
    public async Task ApplyReservationCreatedValidReservationIncreasesReservedAndDecreasesAvailable()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());

        var result = await _projector.ApplyReservationCreatedAsync(reservation);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Balance.OnHandQuantity.Should().Be(10m);
        result.Balance.ReservedQuantity.Should().Be(3m);
        result.Balance.AvailableQuantity.Should().Be(7m);
    }

    [Fact]
    public async Task ApplyReservationCreatedDuplicateCallIsIdempotentReplay()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());

        var first = await _projector.ApplyReservationCreatedAsync(reservation);
        var second = await _projector.ApplyReservationCreatedAsync(reservation);

        first.IsIdempotentReplay.Should().BeFalse();
        second.IsIdempotentReplay.Should().BeTrue();
        second.Balance.ReservedQuantity.Should().Be(3m);
        second.Balance.AvailableQuantity.Should().Be(7m);
    }

    [Fact]
    public async Task ApplyReservationTransitionToReleasedDecreasesReservedAndRestoresAvailable()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());
        await _projector.ApplyReservationCreatedAsync(reservation);

        reservation.TransitionTo(PortionReservationStatus.Released, Guid.NewGuid(), "Cancelled order");
        var result = await _projector.ApplyReservationTransitionAsync(reservation, PortionReservationStatus.Reserved);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Balance.ReservedQuantity.Should().Be(0m);
        result.Balance.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task ApplyReservationTransitionToConsumedDecreasesReservedAndRestoresAvailable()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 4m, "portion", Guid.NewGuid());
        await _projector.ApplyReservationCreatedAsync(reservation);

        reservation.TransitionTo(PortionReservationStatus.Consumed, Guid.NewGuid(), "Cooked and served");
        var result = await _projector.ApplyReservationTransitionAsync(reservation, PortionReservationStatus.Reserved);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Balance.ReservedQuantity.Should().Be(0m);
        result.Balance.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task ApplyReservationTransitionToWasteDecreasesReservedAndRestoresAvailable()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 2m, "portion", Guid.NewGuid());
        await _projector.ApplyReservationCreatedAsync(reservation);

        reservation.TransitionTo(PortionReservationStatus.Waste, Guid.NewGuid(), "Dropped on floor");
        var result = await _projector.ApplyReservationTransitionAsync(reservation, PortionReservationStatus.Reserved);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Balance.ReservedQuantity.Should().Be(0m);
        result.Balance.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task ApplyReservationTransitionDuplicateCallIsIdempotentReplay()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());
        await _projector.ApplyReservationCreatedAsync(reservation);

        reservation.TransitionTo(PortionReservationStatus.Released, Guid.NewGuid(), "Cancelled order");
        var first = await _projector.ApplyReservationTransitionAsync(reservation, PortionReservationStatus.Reserved);
        var second = await _projector.ApplyReservationTransitionAsync(reservation, PortionReservationStatus.Reserved);

        first.IsIdempotentReplay.Should().BeFalse();
        second.IsIdempotentReplay.Should().BeTrue();
        second.Balance.ReservedQuantity.Should().Be(0m);
        second.Balance.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task ApplyReservationTransitionFromNonReservedStatusDoesNotApplyDelta()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());
        reservation.TransitionTo(PortionReservationStatus.Consumed, Guid.NewGuid(), "Cooked");

        // Calling with previous status as Consumed (i.e. already past Reserved)
        var result = await _projector.ApplyReservationTransitionAsync(reservation, PortionReservationStatus.Consumed);

        result.IsIdempotentReplay.Should().BeTrue();
        result.Balance.ReservedQuantity.Should().Be(0m);
        result.Balance.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task DetectDriftWhenBalancesMatchReservationsReturnsNoDrift()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());
        _repo.AddReservation(reservation);
        await _projector.ApplyReservationCreatedAsync(reservation);

        var report = await _projector.DetectDriftAsync();

        report.HasDrift.Should().BeFalse();
        report.Drifts.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectDriftWhenBalancesDriftedIdentifiesAndQuantifiesDrift()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 3m, "portion", Guid.NewGuid());
        _repo.AddReservation(reservation);
        await _projector.ApplyReservationCreatedAsync(reservation);

        // Intentionally tamper projected balance: set reserved to 0, available to 10
        await _repo.SetExactReservedBalanceAsync(_stockItemId, _stockLocationId, 0m);

        var report = await _projector.DetectDriftAsync();

        report.HasDrift.Should().BeTrue();
        report.Drifts.Should().HaveCount(1);
        var drift = report.Drifts[0];
        drift.StockItemId.Should().Be(_stockItemId);
        drift.ProjectedReserved.Should().Be(0m);
        drift.ActualReserved.Should().Be(3m);
        drift.ReservedDrift.Should().Be(-3m);
        drift.AvailableDrift.Should().Be(3m);
    }

    [Fact]
    public async Task RebuildReservationBalancesRestoresExactBalancesAndClearsDrift()
    {
        var reservation = PortionReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), _stockItemId, _stockLocationId, 5m, "portion", Guid.NewGuid());
        _repo.AddReservation(reservation);

        // Drift state: projection says 0 reserved
        await _repo.SetExactReservedBalanceAsync(_stockItemId, _stockLocationId, 0m);
        (await _projector.DetectDriftAsync()).HasDrift.Should().BeTrue();

        // Run rebuild
        var rebuildReport = await _projector.RebuildReservationBalancesAsync();
        rebuildReport.TotalActiveReservations.Should().Be(5);
        rebuildReport.TotalBalancesUpdated.Should().Be(1);

        // Drift report now clean!
        var driftReport = await _projector.DetectDriftAsync();
        driftReport.HasDrift.Should().BeFalse();

        var balance = await _repo.GetBalanceAsync(_stockItemId, _stockLocationId);
        balance.Should().NotBeNull();
        balance!.ReservedQuantity.Should().Be(5m);
        balance.AvailableQuantity.Should().Be(5m);
    }
}
