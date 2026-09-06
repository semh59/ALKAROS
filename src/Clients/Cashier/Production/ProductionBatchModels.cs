namespace ALKAROS.Clients.Cashier.Production;

/// <summary>
/// Result of a production UI operation with localized Turkish error presentation (V11-UI-002, V0-CMP-005).
/// </summary>
public sealed class ProductionOperationResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    internal ProductionOperationResult(bool success, string? errorMessage, string? errorCode)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    public static ProductionOperationResult Ok() => new(true, null, null);
    public static ProductionOperationResult Fail(string errorMessage, string? errorCode = null) =>
        new(false, errorMessage, errorCode);

    public static ProductionOperationResult<T> Ok<T>(T value) => new(true, value, null, null);
    public static ProductionOperationResult<T> Fail<T>(string errorMessage, string? errorCode = null) =>
        new(false, default, errorMessage, errorCode);
}

/// <summary>
/// Generic result containing an entity or error (V11-UI-002).
/// </summary>
public sealed class ProductionOperationResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    internal ProductionOperationResult(bool success, T? value, string? errorMessage, string? errorCode)
    {
        Success = success;
        Value = value;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Operator security context for production actions (V1-IAM-002, V11-UI-002).
/// </summary>
public sealed record ProductionOperator(
    Guid OperatorId,
    string FullName,
    IReadOnlySet<string> Permissions)
{
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission);
}

public static class ProductionPermissions
{
    public const string Execute = "Production.Execute";
}

public sealed record StockItemInfo(
    Guid StockItemId,
    string ItemCode,
    string ItemName,
    string UnitCode);

/// <summary>
/// Detailed missing stock shortfall item presented in recoverable UI view (Acceptance Evidence #1).
/// </summary>
public sealed record StockShortfallItem(
    Guid StockItemId,
    string ItemCode,
    string ItemName,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    decimal ShortfallQuantity,
    string UnitCode);

public sealed record ProductionStockEffectPreview(
    Guid StockItemId,
    string ItemCode,
    string ItemName,
    decimal RequiredQuantity,
    string UnitCode,
    decimal AvailableStock,
    bool HasSufficientStock);

public sealed record ProductionOutputSummary(
    Guid OutputStockItemId,
    string ItemName,
    decimal Quantity,
    string UnitCode,
    DateTimeOffset ProducedAt);

public sealed record RecipeIngredientReadOnlyView(
    Guid StockItemId,
    string ItemName,
    decimal Quantity,
    string UnitCode,
    decimal LossPercentage);

/// <summary>
/// Immutable, read-only representation of a recipe version in production UI (Acceptance Evidence #1).
/// </summary>
public sealed record RecipeVersionReadOnlyView(
    Guid VersionId,
    Guid RecipeId,
    string RecipeName,
    int VersionNumber,
    string Status,
    IReadOnlyList<RecipeIngredientReadOnlyView> Ingredients,
    bool IsReadOnly = true);

public sealed record ProductionBatchView(
    Guid Id,
    string BatchNumber,
    Guid RecipeId,
    string RecipeName,
    Guid RecipeVersionId,
    int RecipeVersionNumber,
    decimal PlannedQuantity,
    decimal? ActualQuantity,
    string UnitCode,
    string Status,
    Guid LocationId,
    string LocationName,
    IReadOnlyList<ProductionStockEffectPreview> Consumptions,
    ProductionOutputSummary? Output,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    IReadOnlyList<StockShortfallItem> MissingStock);

public sealed record PlanProductionBatchCommand(
    Guid RecipeId,
    Guid RecipeVersionId,
    decimal PlannedQuantity,
    Guid LocationId,
    string LocationName,
    string? Notes);

public sealed record StartProductionBatchCommand(
    Guid BatchId);

public sealed record CompleteProductionBatchCommand(
    Guid BatchId,
    decimal ActualQuantity,
    Guid DestinationLocationId);

public sealed record CancelProductionBatchCommand(
    Guid BatchId,
    string Reason);

public sealed record AttemptEditReferencedRecipeVersionCommand(
    Guid RecipeVersionId);

public sealed record ProductionUiAccessibilityMetadata(
    string ElementId,
    string AriaLabel,
    string Role,
    bool IsKeyboardFocusable,
    int MinTargetSizePx,
    string? HelpText,
    bool HighContrastCompliance);
