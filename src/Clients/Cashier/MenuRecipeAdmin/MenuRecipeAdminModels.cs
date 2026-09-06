namespace ALKAROS.Clients.Cashier.MenuRecipeAdmin;

/// <summary>
/// Result of an administrative UI operation with localized Turkish error presentation (V11-UI-001, V0-CMP-005).
/// </summary>
public sealed class AdminOperationResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    internal AdminOperationResult(bool success, string? errorMessage, string? errorCode)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    public static AdminOperationResult Ok() => new(true, null, null);
    public static AdminOperationResult Fail(string errorMessage, string? errorCode = null) =>
        new(false, errorMessage, errorCode);

    public static AdminOperationResult<T> Ok<T>(T value) => new(true, value, null, null);
    public static AdminOperationResult<T> Fail<T>(string errorMessage, string? errorCode = null) =>
        new(false, default, errorMessage, errorCode);
}

/// <summary>
/// Generic result containing an entity or error (V11-UI-001).
/// </summary>
public sealed class AdminOperationResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }

    internal AdminOperationResult(bool success, T? value, string? errorMessage, string? errorCode)
    {
        Success = success;
        Value = value;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Operator security context for administrative actions (V1-IAM-002, V11-UI-001).
/// </summary>
public sealed record MenuRecipeAdminOperator(
    Guid OperatorId,
    string FullName,
    IReadOnlySet<string> Permissions)
{
    public bool HasPermission(string permission) =>
        Permissions.Contains(permission);
}

public static class MenuRecipePermissions
{
    public const string MenuAdmin = "Menu.Admin";
    public const string RecipeAdmin = "Recipe.Admin";
}

#region Static Menu Models

public sealed record StaticMenuView(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<StaticMenuItemViewModel> Items);

public sealed record StaticMenuItemViewModel(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal Price,
    string? Category,
    int DisplayOrder,
    bool IsAvailable);

public sealed record CreateStaticMenuCommand(
    string Code,
    string Name,
    string? Description);

public sealed record AddStaticMenuItemCommand(
    Guid MenuId,
    Guid ProductId,
    string ProductName,
    decimal Price,
    string? Category,
    int DisplayOrder);

public sealed record UpdateStaticMenuItemCommand(
    Guid MenuId,
    Guid ItemId,
    decimal Price,
    int DisplayOrder,
    bool IsAvailable);

#endregion

#region Daily Menu Models

public sealed record DailyMenuView(
    Guid Id,
    DateOnly BusinessDate,
    string MealPeriod,
    string Status,
    IReadOnlyList<DailyMenuItemViewModel> Items);

public sealed record DailyMenuItemViewModel(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid RecipeVersionId,
    int RecipeVersionNumber,
    int PlannedPortions,
    decimal Price,
    int RemainingPortions,
    bool IsSoldOut);

public sealed record CreateDailyMenuCommand(
    DateOnly BusinessDate,
    string MealPeriod);

public sealed record AddDailyMenuItemCommand(
    Guid DailyMenuId,
    Guid ProductId,
    string ProductName,
    Guid RecipeVersionId,
    int RecipeVersionNumber,
    int PlannedPortions,
    decimal Price);

public sealed record PublishDailyMenuCommand(
    Guid DailyMenuId);

#endregion

#region Recipe and Versioning Models

public sealed record RecipeView(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string YieldUnitCode,
    decimal YieldQuantity,
    RecipeVersionViewModel? ActiveVersion,
    IReadOnlyList<RecipeVersionViewModel> Versions);

public sealed record RecipeVersionViewModel(
    Guid Id,
    Guid RecipeId,
    int VersionNumber,
    string Status,
    bool IsReferencedInProductionOrMenu,
    IReadOnlyList<RecipeIngredientViewModel> Ingredients,
    string? ChangeReason,
    DateTimeOffset CreatedAt);

public sealed record RecipeIngredientViewModel(
    Guid Id,
    Guid IngredientItemId,
    string IngredientName,
    decimal Quantity,
    string UnitCode,
    string UnitDimension,
    decimal LossPercentage,
    int SortOrder);

public sealed record RecipeIngredientInput(
    Guid IngredientItemId,
    string IngredientName,
    decimal Quantity,
    string UnitCode,
    string UnitDimension,
    decimal LossPercentage,
    int SortOrder);

public sealed record StockItemUnitInfo(
    Guid StockItemId,
    string ItemCode,
    string ItemName,
    string BaseUnitCode,
    string UnitDimension);

public sealed record CreateRecipeDraftCommand(
    Guid RecipeId,
    string ChangeReason,
    IReadOnlyList<RecipeIngredientInput> Ingredients);

public sealed record EditRecipeVersionCommand(
    Guid RecipeVersionId,
    IReadOnlyList<RecipeIngredientInput> Ingredients);

public sealed record ActivateRecipeVersionCommand(
    Guid RecipeVersionId);

#endregion

#region Accessibility Models (V0-CMP-005)

public sealed record AccessibilityUiMetadata(
    string ElementId,
    string AriaLabel,
    string Role,
    bool IsKeyboardFocusable,
    int MinTargetSizePx,
    string? HelpText,
    bool HighContrastCompliance);

#endregion
