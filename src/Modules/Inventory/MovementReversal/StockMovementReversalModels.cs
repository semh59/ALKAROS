using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;

namespace ALKAROS.Inventory.MovementReversal;

public sealed record StockMovementReversalRequest(
    Guid OriginalMovementId,
    string Reason,
    Guid? ActorId = null);

public sealed record StockMovementReversalResult(
    StockMovement ReversalMovement,
    StockMovement OriginalMovement,
    StockBalance RestoredBalance);
