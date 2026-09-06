namespace ALKAROS.Clients.Cashier.InventoryPurchasing;

/// <summary>
/// Result of an inventory or purchasing UI operation with localized Turkish error presentation (V11-UI-003, V0-CMP-005).
/// </summary>
public sealed class InvPurchOperationResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    internal InvPurchOperationResult(bool success, string? errorMessage, string? errorCode)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    public static InvPurchOperationResult Ok() => new(true, null, null);
    public static InvPurchOperationResult Fail(string errorMessage, string? errorCode = null) =>
        new(false, errorMessage, errorCode);

    public static InvPurchOperationResult<T> Ok<T>(T value) => new(true, value, null, null);
    public static InvPurchOperationResult<T> Fail<T>(string errorMessage, string? errorCode = null) =>
        new(false, default, errorMessage, errorCode);
}

/// <summary>
/// Generic result containing an entity or error (V11-UI-003).
/// </summary>
public sealed class InvPurchOperationResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    internal InvPurchOperationResult(bool success, T? value, string? errorMessage, string? errorCode)
    {
        Success = success;
        Value = value;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Operator security context for inventory and purchasing actions (V1-IAM-002, V11-UI-003).
/// </summary>
public sealed record InventoryPurchasingOperator(
    Guid OperatorId,
    string FullName,
    IReadOnlySet<string> Permissions)
{
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission);
}

public static class InvPurchPermissions
{
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string PurchasingReceive = "Purchasing.Receive";
}

/// <summary>
/// Read-only projected stock balance by location. Immutable projection from movements (Acceptance Evidence #1).
/// </summary>
public sealed record StockBalanceView(
    Guid StockItemId,
    string ItemCode,
    string ItemName,
    Guid LocationId,
    string LocationName,
    decimal OnHandBalance,
    decimal ReservedBalance,
    decimal AvailableBalance,
    string UnitCode,
    long RowVersion,
    DateTimeOffset LastProjectedAt);

#region Purchasing Models

public sealed record PurchaseOrderView(
    Guid OrderId,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    string Status,
    long RowVersion,
    IReadOnlyList<PurchaseOrderItemView> Lines);

public sealed record PurchaseOrderItemView(
    Guid LineId,
    Guid StockItemId,
    string ItemName,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    string UnitCode,
    decimal UnitCost);

public sealed record GoodsReceiptLineView(
    Guid StockItemId,
    string ItemName,
    decimal Quantity,
    string UnitCode,
    decimal UnitCost);

public sealed record GoodsReceiptView(
    Guid ReceiptId,
    Guid PurchaseOrderId,
    string ReceiptNumber,
    Guid LocationId,
    IReadOnlyList<GoodsReceiptLineView> Lines,
    DateTimeOffset ReceivedAt,
    string IdempotencyKey);

public sealed record ReceiveGoodsLineInput(
    Guid StockItemId,
    decimal ReceivedQuantity,
    decimal UnitCost);

public sealed record ReceiveGoodsCommand(
    Guid PurchaseOrderId,
    string ReceiptNumber,
    Guid LocationId,
    IReadOnlyList<ReceiveGoodsLineInput> Lines,
    string IdempotencyKey,
    long ExpectedOrderRowVersion);

#endregion

#region Inventory Adjustment and Waste Models

public sealed record InventoryAdjustmentView(
    Guid AdjustmentId,
    Guid StockItemId,
    string ItemName,
    Guid LocationId,
    decimal DeltaQuantity,
    decimal NewOnHandBalance,
    string Reason,
    string IdempotencyKey,
    DateTimeOffset AdjustedAt);

public sealed record SubmitInventoryAdjustmentCommand(
    Guid StockItemId,
    Guid LocationId,
    decimal DeltaQuantity,
    string Reason,
    string IdempotencyKey,
    long ExpectedRowVersion);

public sealed record WasteRecordView(
    Guid WasteId,
    Guid StockItemId,
    string ItemName,
    Guid LocationId,
    decimal Quantity,
    string UnitCode,
    string WasteReason,
    string IdempotencyKey,
    DateTimeOffset RecordedAt);

public sealed record RecordWasteCommand(
    Guid StockItemId,
    Guid LocationId,
    decimal Quantity,
    string WasteReason,
    string IdempotencyKey,
    long ExpectedRowVersion);

public sealed record AttemptDirectBalanceMutationCommand(
    Guid StockItemId,
    Guid LocationId,
    decimal ArbitraryBalance);

#endregion

#region Accessibility Models (V0-CMP-005)

public sealed record InvPurchUiAccessibilityMetadata(
    string ElementId,
    string AriaLabel,
    string Role,
    bool IsKeyboardFocusable,
    int MinTargetSizePx,
    string? HelpText,
    bool HighContrastCompliance);

#endregion
