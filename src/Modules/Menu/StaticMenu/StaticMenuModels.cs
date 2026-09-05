namespace ALKAROS.Menu.StaticMenu;

public sealed record CreateMenuCommand(string Code, string Name);

public sealed record UpdateMenuCommand(Guid MenuId, string Name, bool IsActive);

public sealed record AddMenuItemCommand(Guid MenuId, Guid ProductId, int DisplayOrder = 0);

public sealed record UpdateMenuItemCommand(Guid MenuItemId, int DisplayOrder, bool IsActive);

public sealed record ReorderMenuItemsCommand(Guid MenuId, IReadOnlyList<Guid> OrderedMenuItemIds);

public sealed record CatalogProductInfo(Guid ProductId, string Name, bool IsActive);

public sealed record MenuItemDetail(
    Guid MenuItemId,
    Guid MenuId,
    Guid ProductId,
    string ProductName,
    bool IsProductActiveInCatalog,
    int DisplayOrder,
    bool IsActive);

public sealed record MenuComposition(
    Menu Menu,
    IReadOnlyList<MenuItemDetail> Items);
