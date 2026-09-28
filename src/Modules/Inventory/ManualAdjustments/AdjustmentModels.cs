using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;

namespace ALKAROS.Inventory.ManualAdjustments;

public enum AdjustmentDirection
{
    Increase,
    Decrease
}

public sealed record InventoryAdjustmentRequest(
    Guid StockItemId,
    Guid StockLocationId,
    AdjustmentDirection Direction,
    decimal Quantity,
    string UnitCode,
    string Reason,
    Guid AuthorizedBy,
    string? IdempotencyKey = null,
    string? Notes = null);

/// <param name="WasReplayed">
/// V1-RMD-422: true when the request's idempotency key had already been applied; <see cref="Movement"/> is then the
/// original movement and both on-hand quantities are the current balance.
/// </param>
public sealed record InventoryAdjustmentResult(
    StockMovement Movement,
    StockBalance UpdatedBalance,
    decimal PreviousOnHandQuantity,
    decimal NewOnHandQuantity,
    bool WasReplayed = false);
