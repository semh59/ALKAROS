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
