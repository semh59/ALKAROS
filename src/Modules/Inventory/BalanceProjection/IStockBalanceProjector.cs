using ALKAROS.Inventory.MovementLedger;

namespace ALKAROS.Inventory.BalanceProjection;

public readonly record struct BalanceRebuildReport(
    int TotalMovementsProcessed,
    int TotalBalancesUpdated,
    TimeSpan Duration);

public interface IStockBalanceProjector
{
    Task<StockBalance?> ApplyMovementAsync(StockMovement movement, CancellationToken ct = default);
    Task<BalanceRebuildReport> RebuildAllBalancesAsync(CancellationToken ct = default);
    Task<decimal> ReplayBalanceForItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default);
}
