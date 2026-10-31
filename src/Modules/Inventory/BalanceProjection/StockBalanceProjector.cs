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

        // Aggregate the ledger BEFORE touching the balances table: a failure here must leave the existing
        // balances intact, and the database-side SUM has no unpaged-row cap.
        var sums = await _movementRepo.GetLedgerBalanceSumsAsync(ct);

        // Reservations are not in the ledger; the repository carries them across the rebuild.
        await _balanceRepo.ReplaceAllBalancesAsync(sums, ct);
        var totalMovements = sums.Sum(x => x.MovementCount);
        var totalBalancesUpdated = sums.Count;

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
