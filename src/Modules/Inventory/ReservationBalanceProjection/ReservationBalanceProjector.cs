using System.Diagnostics;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public sealed class ReservationBalanceProjector : IReservationBalanceProjector
{
    private readonly IReservationBalanceRepository _balanceRepo;

    public ReservationBalanceProjector(IReservationBalanceRepository balanceRepo)
    {
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
    }

    public async Task<ApplyReservationResult> ApplyReservationCreatedAsync(PortionReservation reservation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        if (reservation.Quantity <= 0)
            throw new InvalidReservationBalanceDeltaException($"Reservation quantity must be positive, got {reservation.Quantity}.");

        return await _balanceRepo.ApplyReservationCreatedAtomicAsync(reservation, ct);
    }

    public async Task<ApplyReservationResult> ApplyReservationTransitionAsync(
        PortionReservation reservation,
        PortionReservationStatus previousStatus,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        // If not transitioning from Reserved status, the reservation's reserved balance was already previously released
        if (previousStatus != PortionReservationStatus.Reserved)
        {
            var existing = await _balanceRepo.GetBalanceAsync(reservation.StockItemId, reservation.StockLocationId, ct);
            return new ApplyReservationResult(
                existing ?? StockBalance.Create(reservation.StockItemId, reservation.StockLocationId),
                IsIdempotentReplay: true);
        }

        // Must transition to terminal state
        if (reservation.Status != PortionReservationStatus.Released &&
            reservation.Status != PortionReservationStatus.Consumed &&
            reservation.Status != PortionReservationStatus.Waste)
        {
            throw new InvalidReservationBalanceDeltaException(
                $"Reservation transition status must be terminal (Released, Consumed, Waste), got '{reservation.Status}'.");
        }

        return await _balanceRepo.ApplyReservationTerminalAtomicAsync(reservation, reservation.Status, ct);
    }

    public async Task<ReservationBalanceRebuildReport> RebuildReservationBalancesAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Aggregate active reservations directly from authoritative portion_reservations
        var activeSums = await _balanceRepo.AggregateActiveReservationsAsync(ct);

        // 2. Fetch all existing stock balances
        var balances = await _balanceRepo.GetAllBalancesAsync(ct);

        // 3. Reset applied projection events to prepare clean state
        await _balanceRepo.ResetAppliedEventsAsync(ct);

        // 4. Update all balance records with exact reserved quantity
        var processedKeys = new HashSet<(Guid Item, Guid Location)>();
        var updatedCount = 0;

        foreach (var balance in balances)
        {
            var key = (balance.StockItemId, balance.StockLocationId);
            processedKeys.Add(key);
            var activeQty = activeSums.GetValueOrDefault(key, 0m);

            await _balanceRepo.SetExactReservedBalanceAsync(key.StockItemId, key.StockLocationId, activeQty, ct);
            updatedCount++;
        }

        // Also cover active reservations for items/locations with no stock_balance row yet
        foreach (var (key, activeQty) in activeSums)
        {
            if (!processedKeys.Contains(key))
            {
                await _balanceRepo.SetExactReservedBalanceAsync(key.StockItemId, key.StockLocationId, activeQty, ct);
                updatedCount++;
            }
        }

        sw.Stop();
        var totalActive = activeSums.Values.Sum();
        return new ReservationBalanceRebuildReport((int)totalActive, updatedCount, sw.Elapsed);
    }

    public async Task<ReservationBalanceDriftReport> DetectDriftAsync(CancellationToken ct = default)
    {
        var balances = await _balanceRepo.GetAllBalancesAsync(ct);
        var activeSums = await _balanceRepo.AggregateActiveReservationsAsync(ct);

        var drifts = new List<ReservationBalanceDrift>();
        var checkedKeys = new HashSet<(Guid Item, Guid Location)>();

        foreach (var b in balances)
        {
            var key = (b.StockItemId, b.StockLocationId);
            checkedKeys.Add(key);

            var actualReserved = activeSums.GetValueOrDefault(key, 0m);
            var expectedAvailable = b.OnHandQuantity - actualReserved;

            if (b.ReservedQuantity != actualReserved || b.AvailableQuantity != expectedAvailable)
            {
                drifts.Add(new ReservationBalanceDrift(
                    StockItemId: b.StockItemId,
                    StockLocationId: b.StockLocationId,
                    ProjectedReserved: b.ReservedQuantity,
                    ActualReserved: actualReserved,
                    ReservedDrift: b.ReservedQuantity - actualReserved,
                    ProjectedAvailable: b.AvailableQuantity,
                    ExpectedAvailable: expectedAvailable,
                    AvailableDrift: b.AvailableQuantity - expectedAvailable));
            }
        }

        foreach (var (key, actualReserved) in activeSums)
        {
            if (!checkedKeys.Contains(key) && actualReserved > 0)
            {
                drifts.Add(new ReservationBalanceDrift(
                    StockItemId: key.StockItemId,
                    StockLocationId: key.StockLocationId,
                    ProjectedReserved: 0m,
                    ActualReserved: actualReserved,
                    ReservedDrift: -actualReserved,
                    ProjectedAvailable: 0m,
                    ExpectedAvailable: -actualReserved,
                    AvailableDrift: actualReserved));
            }
        }

        return new ReservationBalanceDriftReport(drifts.Count > 0, drifts);
    }
}
