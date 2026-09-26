using System.Diagnostics;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;

namespace ALKAROS.Inventory.BalanceProjection;

public sealed class StockBalanceProjector : IStockBalanceProjector
{
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly IStockLocationRepository _locationRepo;

    public StockBalanceProjector(
        IStockBalanceRepository balanceRepo,
        IStockMovementRepository movementRepo,
        IStockLocationRepository locationRepo)
    {
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        _locationRepo = locationRepo ?? throw new ArgumentNullException(nameof(locationRepo));
    }

    public async Task<StockBalance?> ApplyMovementAsync(
        StockMovement movement,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(movement);

        var delta = movement.Effect.OnHandDelta;
        return await _balanceRepo.ApplyOnHandDeltaAsync(
            movement.StockItemId,
            movement.StockLocationId,
            delta,
            ct);
    }

    public async Task<BalanceRebuildReport> RebuildAllBalancesAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // V1-RMD-320 (independent 2026-09-26 audit, finding K8): captured BEFORE the reset below wipes the
        // whole table. ResetAllBalancesAsync's DELETE, followed by SetExactBalanceAsync's own always-a-
        // fresh-INSERT (reserved_quantity defaults to 0), used to silently zero every active reservation on
        // every rebuild - unreachable from any HTTP endpoint/hosted service today, but a future maintenance
        // tool wiring this up would have made every reserved portion look "available" again, a real
        // double-sell risk. Restored at the end of this method, not the caller's problem to remember.
        var reservedBefore = await _balanceRepo.GetAllReservedQuantitiesAsync(ct);

        // 1. Reset all projected balances to guarantee no ghost or orphaned balances
        await _balanceRepo.ResetAllBalancesAsync(ct);

        // 2. Iterate across all locations and replay their ledger movements
        var locations = await _locationRepo.GetAllAsync(activeOnly: false, ct);
        var totalMovements = 0;
        var totalBalancesUpdated = 0;

        foreach (var loc in locations)
        {
            var movements = await _movementRepo.GetByLocationAsync(loc.Id, ct);
            if (movements.Count == 0)
                continue;

            totalMovements += movements.Count;

            // Group movements by StockItemId and sum OnHandDelta
            var itemSums = movements
                .GroupBy(m => m.StockItemId)
                .Select(g => new
                {
                    StockItemId = g.Key,
                    OnHand = g.Sum(m => m.Effect.OnHandDelta)
                });

            foreach (var item in itemSums)
            {
                await _balanceRepo.SetExactBalanceAsync(item.StockItemId, loc.Id, item.OnHand, ct);
                totalBalancesUpdated++;
            }
        }

        // 3. Restore every reservation captured in step 0 - whether or not the pair was touched by the
        // on-hand rebuild above (a reservation can exist for an item/location with no net movement
        // history at all, e.g. every movement that created it was later fully reversed).
        foreach (var ((stockItemId, stockLocationId), reservedQuantity) in reservedBefore)
            await _balanceRepo.RestoreReservedQuantityAsync(stockItemId, stockLocationId, reservedQuantity, ct);

        sw.Stop();
        return new BalanceRebuildReport(totalMovements, totalBalancesUpdated, sw.Elapsed);
    }

    public async Task<decimal> ReplayBalanceForItemAndLocationAsync(
        Guid stockItemId,
        Guid stockLocationId,
        CancellationToken ct = default)
    {
        var movements = await _movementRepo.GetByLocationAsync(stockLocationId, ct);
        return movements
            .Where(m => m.StockItemId == stockItemId)
            .Sum(m => m.Effect.OnHandDelta);
    }
}
