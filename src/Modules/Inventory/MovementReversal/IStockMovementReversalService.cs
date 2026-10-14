using ALKAROS.Inventory.MovementLedger;
using Npgsql;

namespace ALKAROS.Inventory.MovementReversal;

/// <summary>
/// Put back <paramref name="ReturnedQuantity"/> more of the <paramref name="TotalQuantity"/> units an order item
/// consumed, <paramref name="PreviouslyReturnedQuantity"/> of which were already put back: every consumption of that
/// item returns the matching share of its ingredient.
/// </summary>
public sealed record StockShareReturnRequest(
    Guid OrderItemId,
    decimal ReturnedQuantity,
    decimal PreviouslyReturnedQuantity,
    decimal TotalQuantity,
    string Reason,
    Guid? ActorId = null);

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

    /// <summary>
    /// Writes one <c>Return</c> movement per consumed ingredient of the order item for the returned share of its
    /// quantity, and raises the balances, on the caller's connection and transaction. The sum returned for an
    /// ingredient follows the cumulative share of the units returned, so rounding never drifts and the sum never exceeds what the item consumed, and
    /// consumptions that were already reversed as a whole are left alone. The caller serialises concurrent returns
    /// of the same item.
    /// </summary>
    Task<IReadOnlyList<StockMovement>> ReturnConsumedShareAsync(
        StockShareReturnRequest request,
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
