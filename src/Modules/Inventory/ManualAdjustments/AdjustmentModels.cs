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

public sealed record InventoryAdjustmentResult(
    StockMovement Movement,
    StockBalance UpdatedBalance,
    decimal PreviousOnHandQuantity,
    decimal NewOnHandQuantity);
