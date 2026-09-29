using System.Globalization;
using ALKAROS.Menu.DailyMenuLifecycle;
using ALKAROS.Menu.StaticMenu;

namespace ALKAROS.Host.Experience.Menu;

// ---- Static menu (persistent, named menus composed of catalog products) ----

public sealed record CreateMenuV1(string Code, string Name);

public sealed record UpdateMenuV1(string Name, bool IsActive);

public sealed record AddMenuItemV1(Guid ProductId, int DisplayOrder = 0);

public sealed record UpdateMenuItemV1(int DisplayOrder, bool IsActive);

public sealed record ReorderMenuItemsV1(IReadOnlyList<Guid> OrderedMenuItemIds);

public sealed record MenuV1(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static MenuV1 From(ALKAROS.Menu.StaticMenu.Menu value)
        => new(value.Id, value.Code, value.Name, value.IsActive, value.CreatedAt, value.UpdatedAt);
}

public sealed record MenuItemDetailV1(
    Guid MenuItemId,
    Guid MenuId,
    Guid ProductId,
    string ProductName,
    bool IsProductActiveInCatalog,
    int DisplayOrder,
    bool IsActive)
{
    public static MenuItemDetailV1 From(MenuItemDetail value)
        => new(value.MenuItemId, value.MenuId, value.ProductId, value.ProductName,
            value.IsProductActiveInCatalog, value.DisplayOrder, value.IsActive);
}

public sealed record MenuCompositionV1(MenuV1 Menu, IReadOnlyList<MenuItemDetailV1> Items)
{
    public static MenuCompositionV1 From(MenuComposition value)
        => new(MenuV1.From(value.Menu), value.Items.Select(MenuItemDetailV1.From).ToArray());
}

public sealed record MenuItemV1(Guid Id, Guid MenuId, Guid ProductId, int DisplayOrder, bool IsActive)
{
    public static MenuItemV1 From(ALKAROS.Menu.StaticMenu.MenuItem value)
        => new(value.Id, value.MenuId, value.ProductId, value.DisplayOrder, value.IsActive);
}

// ---- Daily menu (a single service day's specials, with portion tracking) ----

public sealed record CreateDailyMenuV1(DateOnly? BusinessDate, string? Note);

public sealed record OpenDailyMenuV1(DateTimeOffset? OpenedAt);

public sealed record CloseDailyMenuV1(DateTimeOffset? ClosedAt);

public sealed record AddDailyMenuItemV1(
    Guid ProductId,
    decimal? Price,
    Guid? RecipeVersionId,
    decimal PlannedPortions,
    string? PrinterRoutePolicy);

public sealed record UpdateDailyMenuItemPriceV1(decimal NewPrice);

public sealed record UpdateDailyMenuItemPlannedPortionsV1(decimal NewPlannedPortions);

public sealed record UpdateDailyMenuItemStatusV1(bool IsActive);

public sealed record DailyMenuV1(
    Guid Id,
    string BusinessDate,
    string Status,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt,
    Guid? ClosedBy,
    string? Note,
    int RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DailyMenuV1 From(DailyMenu value)
        => new(value.Id, value.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), value.Status.ToString(),
            value.OpenedAt, value.ClosedAt, value.ClosedBy, value.Note, value.RowVersion,
            value.CreatedAt, value.UpdatedAt);
}

public sealed record DailyMenuItemV1(
    Guid Id,
    Guid DailyMenuId,
    Guid ProductId,
    string ProductNameSnapshot,
    Guid? RecipeVersionId,
    decimal Price,
    decimal PlannedPortions,
    decimal PreparedPortions,
    decimal AvailablePortions,
    decimal ReservedPortions,
    decimal ConsumedPortions,
    decimal WastePortions,
    bool IsOutOfStock,
    string? PrinterRoutePolicy,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DailyMenuItemV1 From(DailyMenuItem value)
        => new(value.Id, value.DailyMenuId, value.ProductId, value.ProductNameSnapshot,
            value.RecipeVersionId, value.Price, value.PlannedPortions, value.PreparedPortions,
            value.AvailablePortions, value.ReservedPortions, value.ConsumedPortions, value.WastePortions,
            value.IsOutOfStock, value.PrinterRoutePolicy, value.IsActive, value.CreatedAt, value.UpdatedAt);
}

public sealed record DailyMenuDetailsV1(DailyMenuV1 Menu, IReadOnlyList<DailyMenuItemV1> Items)
{
    public static DailyMenuDetailsV1 From(DailyMenuDetails value)
        => new(DailyMenuV1.From(value.Menu), value.Items.Select(DailyMenuItemV1.From).ToArray());
}

