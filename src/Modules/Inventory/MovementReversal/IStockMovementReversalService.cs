using ALKAROS.Inventory.MovementLedger;
using Npgsql;

namespace ALKAROS.Inventory.MovementReversal;

public interface IStockMovementReversalService
{
    Task<StockMovementReversalResult> ReverseMovementAsync(
        StockMovementReversalRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-424: same as <see cref="ReverseMovementAsync(StockMovementReversalRequest, CancellationToken)"/>, with the
    /// reversal movement and its balance change written on the caller's connection and transaction, so they commit
    /// or roll back with the caller's own writes. The caller owns the transaction.
    /// </summary>
    Task<StockMovementReversalResult> ReverseMovementAsync(
        StockMovementReversalRequest request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default);

    Task<bool> CanReverseAsync(
        Guid movementId,
        CancellationToken ct = default);

    Task<StockMovement?> GetReversalForMovementAsync(
        Guid originalMovementId,
        CancellationToken ct = default);
}
