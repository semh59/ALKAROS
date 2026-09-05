using ALKAROS.Inventory.MovementLedger;

namespace ALKAROS.Inventory.MovementReversal;

public interface IStockMovementReversalService
{
    Task<StockMovementReversalResult> ReverseMovementAsync(
        StockMovementReversalRequest request,
        CancellationToken ct = default);

    Task<bool> CanReverseAsync(
        Guid movementId,
        CancellationToken ct = default);

    Task<StockMovement?> GetReversalForMovementAsync(
        Guid originalMovementId,
        CancellationToken ct = default);
}
