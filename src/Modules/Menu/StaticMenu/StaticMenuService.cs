namespace ALKAROS.Menu.StaticMenu;

public sealed class StaticMenuService : IStaticMenuService
{
    private readonly IMenuRepository _menuRepo;
    private readonly IMenuItemRepository _menuItemRepo;
    private readonly ICatalogProductReader _catalogReader;

    public StaticMenuService(
        IMenuRepository menuRepo,
        IMenuItemRepository menuItemRepo,
        ICatalogProductReader catalogReader)
    {
        _menuRepo = menuRepo ?? throw new ArgumentNullException(nameof(menuRepo));
        _menuItemRepo = menuItemRepo ?? throw new ArgumentNullException(nameof(menuItemRepo));
        _catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
    }

    public async Task<Menu> CreateMenuAsync(CreateMenuCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Code))
            throw new InvalidMenuCommandException("Menu code cannot be empty.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new InvalidMenuCommandException("Menu name cannot be empty.");

        var existing = await _menuRepo.GetByCodeAsync(command.Code, ct);
        if (existing != null)
            throw new DuplicateMenuCodeException(command.Code);

        var menu = Menu.Create(command.Code, command.Name);
        await _menuRepo.InsertAsync(menu, ct);
        return menu;
    }

    public async Task<Menu> UpdateMenuAsync(UpdateMenuCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.MenuId == Guid.Empty)
            throw new InvalidMenuCommandException("MenuId cannot be empty.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new InvalidMenuCommandException("Menu name cannot be empty.");

        var menu = await _menuRepo.GetByIdAsync(command.MenuId, ct)
            ?? throw new MenuNotFoundException(command.MenuId);

        menu.Update(command.Name, command.IsActive);
        await _menuRepo.UpdateAsync(menu, ct);
        return menu;
    }

    public async Task<MenuItem> AddMenuItemAsync(AddMenuItemCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.MenuId == Guid.Empty)
            throw new InvalidMenuCommandException("MenuId cannot be empty.");

        if (command.ProductId == Guid.Empty)
            throw new InvalidMenuCommandException("ProductId cannot be empty.");

        if (command.DisplayOrder < 0)
            throw new InvalidMenuCommandException("DisplayOrder cannot be negative.");

        var menu = await _menuRepo.GetByIdAsync(command.MenuId, ct)
            ?? throw new MenuNotFoundException(command.MenuId);

        var catalogProduct = await _catalogReader.GetProductAsync(command.ProductId, ct)
            ?? throw new CatalogProductNotFoundException(command.ProductId);

        var existingItem = await _menuItemRepo.GetByMenuAndProductAsync(command.MenuId, command.ProductId, ct);
        if (existingItem != null)
            throw new DuplicateMenuItemProductException(command.MenuId, command.ProductId);

        var item = MenuItem.Create(command.MenuId, command.ProductId, command.DisplayOrder);
        await _menuItemRepo.InsertAsync(item, ct);
        return item;
    }

    public async Task<MenuItem> UpdateMenuItemAsync(UpdateMenuItemCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.MenuItemId == Guid.Empty)
            throw new InvalidMenuCommandException("MenuItemId cannot be empty.");

        if (command.DisplayOrder < 0)
            throw new InvalidMenuCommandException("DisplayOrder cannot be negative.");

        var item = await _menuItemRepo.GetByIdAsync(command.MenuItemId, ct)
            ?? throw new MenuItemNotFoundException(command.MenuItemId);

        item.UpdateOrder(command.DisplayOrder);
        item.UpdateStatus(command.IsActive);
        await _menuItemRepo.UpdateAsync(item, ct);
        return item;
    }

    public async Task ReorderMenuItemsAsync(ReorderMenuItemsCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.MenuId == Guid.Empty)
            throw new InvalidMenuCommandException("MenuId cannot be empty.");

        if (command.OrderedMenuItemIds == null || command.OrderedMenuItemIds.Count == 0)
            throw new InvalidMenuCommandException("OrderedMenuItemIds cannot be empty.");

        var menu = await _menuRepo.GetByIdAsync(command.MenuId, ct)
            ?? throw new MenuNotFoundException(command.MenuId);

        await _menuItemRepo.ReorderAsync(command.MenuId, command.OrderedMenuItemIds, ct);
    }

    public async Task<MenuComposition> GetMenuCompositionAsync(Guid menuId, bool activeOnly = false, CancellationToken ct = default)
    {
        if (menuId == Guid.Empty)
            throw new InvalidMenuCommandException("MenuId cannot be empty.");

        var menu = await _menuRepo.GetByIdAsync(menuId, ct)
            ?? throw new MenuNotFoundException(menuId);

        var items = await _menuItemRepo.GetByMenuIdAsync(menuId, activeOnly, ct);

        var productIds = items.Select(i => i.ProductId).Distinct();
        var catalogMap = await _catalogReader.GetProductsAsync(productIds, ct);

        var details = items.Select(item =>
        {
            catalogMap.TryGetValue(item.ProductId, out var productInfo);
            var productName = productInfo?.Name ?? "Unknown Product";
            var isProductActiveInCatalog = productInfo?.IsActive ?? false;

            return new MenuItemDetail(
                MenuItemId: item.Id,
                MenuId: item.MenuId,
                ProductId: item.ProductId,
                ProductName: productName,
                IsProductActiveInCatalog: isProductActiveInCatalog,
                DisplayOrder: item.DisplayOrder,
                IsActive: item.IsActive);
        }).OrderBy(d => d.DisplayOrder).ToList();

        return new MenuComposition(menu, details);
    }

    public async Task<IReadOnlyList<Menu>> GetAllMenusAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        return await _menuRepo.GetAllAsync(activeOnly, ct);
    }
}
