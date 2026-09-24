using ALKAROS.Inventory.PhysicalCounts;
using ALKAROS.Inventory.StockMaster;

namespace ALKAROS.Host.Experience.Inventory;

public sealed record CreateStockLocationV1(string Code, string Name, string LocationType, bool IsActive = true);

public sealed record CreateStockItemV1(
    string Code, string Name, string ItemType, string TrackingUnitCode,
    Guid? DefaultLocationId = null, bool IsActive = true, decimal? ReorderPoint = null);

public sealed record AssignProductStockMappingV1(Guid StockItemId, decimal QuantityMultiplier = 1.0m, string? Notes = null);

public sealed record StockLocationV1(Guid Id, string Code, string Name, string LocationType, bool IsActive)
{
    public static StockLocationV1 From(StockLocation value)
        => new(value.Id, value.Code, value.Name, value.LocationType.ToString(), value.IsActive);
}

public sealed record StockItemV1(
    Guid Id, string Code, string Name, string ItemType, string TrackingUnitCode, Guid? DefaultLocationId, bool IsActive,
    decimal? ReorderPoint)
{
    public static StockItemV1 From(StockItem value)
        => new(value.Id, value.Code, value.Name, value.ItemType.ToString(), value.TrackingUnitCode, value.DefaultLocationId, value.IsActive, value.ReorderPoint);
}

/// <summary>V11-INV-009: set (or clear, with null) a stock item's persisted low-stock threshold.</summary>
public sealed record SetReorderPointV1(decimal? ReorderPoint);

/// <summary>
/// <paramref name="AvailableQuantity"/> is null when the mapped stock item
/// has no balance row yet (never received/produced) — distinct from zero,
/// which means a real, tracked, currently-empty balance.
/// </summary>
public sealed record ProductStockMappingV1(
    Guid ProductId, Guid StockItemId, string StockItemName, decimal QuantityMultiplier,
    string? Notes, decimal? AvailableQuantity);

/// <summary>V1-RMD-152: assigning a stock item to a modifier.</summary>
public sealed record AssignModifierStockMappingV1(Guid StockItemId, decimal QuantityMultiplier = 1.0m, string? Notes = null);

/// <summary>
/// V1-RMD-152: a modifier's own bill of materials.
/// <paramref name="AvailableQuantity"/> is how many of this modifier the
/// mapped stock could still cover, or null when the item has no default
/// location or balance yet — the same meaning it carries for a product.
/// </summary>
public sealed record ModifierStockMappingV1(
    Guid ModifierId, Guid StockItemId, decimal QuantityMultiplier,
    string? Notes, decimal? AvailableQuantity);

/// <summary>V11-INV-008: what someone counted on the shelf for a stock item.</summary>
public sealed record RecordPhysicalCountV1(Guid StockLocationId, decimal CountedQuantity, string? Notes = null);

public sealed record PhysicalCountResultV1(
    Guid StockItemId, Guid StockLocationId, decimal CountedQuantity,
    decimal PreviousOnHandQuantity, decimal NewOnHandQuantity, decimal Delta, DateTimeOffset CountedAt)
{
    public static PhysicalCountResultV1 From(PhysicalCountResult result)
        => new(
            result.Count.StockItemId, result.Count.StockLocationId, result.Count.CountedQuantity,
            result.PreviousOnHandQuantity, result.NewOnHandQuantity, result.Count.Delta, result.Count.CountedAt);
}

/// <summary>V1-RMD-274: a manager records waste (spoilage, expiry, damage...) against one stock item at one location.</summary>
public sealed record RecordWasteV1(
    Guid StockLocationId,
    string WasteSource,
    decimal Quantity,
    string UnitCode,
    string Reason,
    Guid? SourceReferenceId = null,
    string? IdempotencyKey = null);

public sealed record WasteRecordV1(
    Guid Id,
    Guid StockMovementId,
    Guid StockItemId,
    Guid StockLocationId,
    string WasteSource,
    Guid? SourceReferenceId,
    decimal Quantity,
    string UnitCode,
    decimal NormalizedQuantity,
    string TrackingUnitCode,
    string Reason,
    Guid RecordedBy,
    DateTimeOffset RecordedAt)
{
    public static WasteRecordV1 From(ALKAROS.Inventory.WasteRecording.WasteRecord record) => new(
        record.Id, record.StockMovementId, record.StockItemId, record.StockLocationId, record.WasteSource,
        record.SourceReferenceId, record.Quantity, record.UnitCode, record.NormalizedQuantity,
        record.TrackingUnitCode, record.WasteReason, record.RecordedBy, record.RecordedAt);
}

public sealed record RecordWasteResultV1(WasteRecordV1 Record, bool IsIdempotentReplay);

public sealed record StockMasterApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record StockMasterApiErrorEnvelopeV1(StockMasterApiErrorV1 Error);
