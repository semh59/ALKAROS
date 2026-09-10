using ALKAROS.Inventory.StockMaster;

namespace ALKAROS.Host.Experience.Inventory;

public sealed record CreateStockLocationV1(string Code, string Name, string LocationType, bool IsActive = true);

public sealed record CreateStockItemV1(
    string Code, string Name, string ItemType, string TrackingUnitCode,
    Guid? DefaultLocationId = null, bool IsActive = true);

public sealed record AssignProductStockMappingV1(Guid StockItemId, decimal QuantityMultiplier = 1.0m, string? Notes = null);

public sealed record StockLocationV1(Guid Id, string Code, string Name, string LocationType, bool IsActive)
{
    public static StockLocationV1 From(StockLocation value)
        => new(value.Id, value.Code, value.Name, value.LocationType.ToString(), value.IsActive);
}

public sealed record StockItemV1(
    Guid Id, string Code, string Name, string ItemType, string TrackingUnitCode, Guid? DefaultLocationId, bool IsActive)
{
    public static StockItemV1 From(StockItem value)
        => new(value.Id, value.Code, value.Name, value.ItemType.ToString(), value.TrackingUnitCode, value.DefaultLocationId, value.IsActive);
}

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

public sealed record StockMasterApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record StockMasterApiErrorEnvelopeV1(StockMasterApiErrorV1 Error);
