namespace ALKAROS.Inventory.MovementLedger;

public static class StockMovementSourceType
{
    public const string PurchaseOrder = "PurchaseOrder";
    public const string GoodsReceipt = "GoodsReceipt";
    public const string ProductionOrder = "ProductionOrder";
    public const string Order = "Order";
    public const string DailyMenu = "DailyMenu";
    public const string WasteRecord = "WasteRecord";
    public const string InventoryAudit = "InventoryAudit";
    public const string StockMovement = "StockMovement";
    public const string Manual = "Manual";

    public static readonly IReadOnlySet<string> ValidSourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PurchaseOrder,
        GoodsReceipt,
        ProductionOrder,
        Order,
        DailyMenu,
        WasteRecord,
        InventoryAudit,
        StockMovement,
        Manual
    };

    public static bool IsValid(string sourceType)
    {
        return !string.IsNullOrWhiteSpace(sourceType) && ValidSourceTypes.Contains(sourceType.Trim());
    }
}
