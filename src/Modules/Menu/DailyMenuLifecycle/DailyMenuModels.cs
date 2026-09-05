namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed record CreateDailyMenuCommand(
    DateOnly? BusinessDate = null,
    string? Note = null);

public sealed record OpenDailyMenuCommand(
    Guid DailyMenuId,
    DateTimeOffset? OpenedAt = null);

public sealed record CloseDailyMenuCommand(
    Guid DailyMenuId,
    Guid ClosedBy,
    DateTimeOffset? ClosedAt = null);

public sealed record AddDailyMenuItemCommand(
    Guid DailyMenuId,
    Guid ProductId,
    decimal? Price = null,
    Guid? RecipeVersionId = null,
    decimal PlannedPortions = 0m,
    string? PrinterRoutePolicy = null,
    Guid? CreatedBy = null);

public sealed record UpdateDailyMenuItemPriceCommand(
    Guid DailyMenuItemId,
    decimal NewPrice,
    Guid? ChangedBy = null);

public sealed record UpdateDailyMenuItemPlannedPortionsCommand(
    Guid DailyMenuItemId,
    decimal NewPlannedPortions,
    Guid? ChangedBy = null);

public sealed record UpdateDailyMenuItemStatusCommand(
    Guid DailyMenuItemId,
    bool IsActive,
    Guid? ChangedBy = null);

public sealed record CatalogProductPriceInfo(
    Guid ProductId,
    string Sku,
    string Name,
    decimal? CurrentPrice,
    bool IsActive);

public sealed record DailyMenuDetails(
    DailyMenu Menu,
    IReadOnlyList<DailyMenuItem> Items);
