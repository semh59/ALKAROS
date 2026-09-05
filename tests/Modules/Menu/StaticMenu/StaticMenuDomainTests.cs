using FluentAssertions;
using Xunit;

namespace ALKAROS.Menu.StaticMenu.Tests;

public sealed class FakeMenuRepository : IMenuRepository
{
    private readonly Dictionary<Guid, Menu> _menus = new();

    public Task InsertAsync(Menu menu, CancellationToken ct = default)
    {
        if (_menus.Values.Any(m => string.Equals(m.Code, menu.Code, StringComparison.OrdinalIgnoreCase)))
            throw new DuplicateMenuCodeException(menu.Code);

        _menus[menu.Id] = menu;
        return Task.CompletedTask;
    }

    public Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_menus.GetValueOrDefault(id));

    public Task<Menu?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Task.FromResult(_menus.Values.FirstOrDefault(m => string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task UpdateAsync(Menu menu, CancellationToken ct = default)
    {
        if (!_menus.ContainsKey(menu.Id))
            throw new MenuNotFoundException(menu.Id);
        _menus[menu.Id] = menu;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Menu>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Menu>>(_menus.Values.Where(m => !activeOnly || m.IsActive).OrderBy(m => m.Code).ToList());
}

public sealed class FakeMenuItemRepository : IMenuItemRepository
{
    private readonly Dictionary<Guid, MenuItem> _items = new();

    public Task InsertAsync(MenuItem item, CancellationToken ct = default)
    {
        if (_items.Values.Any(i => i.MenuId == item.MenuId && i.ProductId == item.ProductId))
            throw new DuplicateMenuItemProductException(item.MenuId, item.ProductId);

        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<MenuItem?> GetByMenuAndProductAsync(Guid menuId, Guid productId, CancellationToken ct = default) =>
        Task.FromResult(_items.Values.FirstOrDefault(i => i.MenuId == menuId && i.ProductId == productId));

    public Task<IReadOnlyList<MenuItem>> GetByMenuIdAsync(Guid menuId, bool activeOnly = false, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MenuItem>>(_items.Values
            .Where(i => i.MenuId == menuId && (!activeOnly || i.IsActive))
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.CreatedAt)
            .ToList());

    public Task UpdateAsync(MenuItem item, CancellationToken ct = default)
    {
        if (!_items.ContainsKey(item.Id))
            throw new MenuItemNotFoundException(item.Id);
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _items.Remove(id);
        return Task.CompletedTask;
    }

    public Task ReorderAsync(Guid menuId, IReadOnlyList<Guid> orderedMenuItemIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedMenuItemIds.Count; i++)
        {
            if (_items.TryGetValue(orderedMenuItemIds[i], out var item) && item.MenuId == menuId)
            {
                item.UpdateOrder(i);
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class FakeCatalogProductReader : ICatalogProductReader
{
    private readonly Dictionary<Guid, CatalogProductInfo> _products = new();

    public void AddProduct(Guid id, string name, bool isActive) =>
        _products[id] = new CatalogProductInfo(id, name, isActive);

    public void SetActive(Guid id, bool isActive)
    {
        if (_products.TryGetValue(id, out var info))
            _products[id] = new CatalogProductInfo(info.ProductId, info.Name, isActive);
    }

    public Task<CatalogProductInfo?> GetProductAsync(Guid productId, CancellationToken ct = default) =>
        Task.FromResult(_products.GetValueOrDefault(productId));

    public Task<IReadOnlyDictionary<Guid, CatalogProductInfo>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        var dict = productIds
            .Distinct()
            .Where(_products.ContainsKey)
            .ToDictionary(id => id, id => _products[id]);
        return Task.FromResult<IReadOnlyDictionary<Guid, CatalogProductInfo>>(dict);
    }
}

public sealed class StaticMenuDomainTests
{
    private readonly FakeMenuRepository _menuRepo;
    private readonly FakeMenuItemRepository _menuItemRepo;
    private readonly FakeCatalogProductReader _catalogReader;
    private readonly StaticMenuService _menuService;

    public StaticMenuDomainTests()
    {
        _menuRepo = new FakeMenuRepository();
        _menuItemRepo = new FakeMenuItemRepository();
        _catalogReader = new FakeCatalogProductReader();
        _menuService = new StaticMenuService(_menuRepo, _menuItemRepo, _catalogReader);
    }

    [Fact]
    public async Task CreateMenuWithValidCommandReturnsActiveMenu()
    {
        var cmd = new CreateMenuCommand("LUNCH_2026", "Lunch Menu 2026");
        var menu = await _menuService.CreateMenuAsync(cmd);

        menu.Should().NotBeNull();
        menu.Code.Should().Be("LUNCH_2026");
        menu.Name.Should().Be("Lunch Menu 2026");
        menu.IsActive.Should().BeTrue();

        var retrieved = await _menuRepo.GetByIdAsync(menu.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Code.Should().Be("LUNCH_2026");
    }

    [Fact]
    public async Task CreateMenuWithDuplicateCodeThrowsDuplicateMenuCodeException()
    {
        await _menuService.CreateMenuAsync(new CreateMenuCommand("DINNER", "Dinner Standard"));
        var act = () => _menuService.CreateMenuAsync(new CreateMenuCommand("DINNER", "Dinner Duplicate"));

        await act.Should().ThrowAsync<DuplicateMenuCodeException>();
    }

    [Theory]
    [InlineData("", "Valid Name")]
    [InlineData("   ", "Valid Name")]
    [InlineData("CODE", "")]
    [InlineData("CODE", "   ")]
    public async Task CreateMenuWithEmptyCodeOrNameThrowsInvalidMenuCommandException(string code, string name)
    {
        var act = () => _menuService.CreateMenuAsync(new CreateMenuCommand(code, name));
        await act.Should().ThrowAsync<InvalidMenuCommandException>();
    }

    [Fact]
    public async Task AddMenuItemWithValidCommandAddsItemToMenu()
    {
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand("MAIN_A", "Main A"));
        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Grilled Salmon", true);

        var item = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId, 1));
        item.Should().NotBeNull();
        item.MenuId.Should().Be(menu.Id);
        item.ProductId.Should().Be(prodId);
        item.DisplayOrder.Should().Be(1);
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddDuplicateProductToMenuThrowsDuplicateMenuItemProductException()
    {
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand("MAIN_B", "Main B"));
        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Ribeye Steak", true);

        await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId, 1));
        var act = () => _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId, 2));

        await act.Should().ThrowAsync<DuplicateMenuItemProductException>();
    }

    [Fact]
    public async Task AddNonExistentCatalogProductThrowsCatalogProductNotFoundException()
    {
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand("MAIN_C", "Main C"));
        var randomProdId = Guid.NewGuid();

        var act = () => _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, randomProdId, 0));
        await act.Should().ThrowAsync<CatalogProductNotFoundException>();
    }

    [Fact]
    public async Task AddMenuItemWithInvalidParametersThrowsInvalidMenuCommandException()
    {
        var act1 = () => _menuService.AddMenuItemAsync(new AddMenuItemCommand(Guid.Empty, Guid.NewGuid()));
        await act1.Should().ThrowAsync<InvalidMenuCommandException>();

        var act2 = () => _menuService.AddMenuItemAsync(new AddMenuItemCommand(Guid.NewGuid(), Guid.Empty));
        await act2.Should().ThrowAsync<InvalidMenuCommandException>();

        var act3 = () => _menuService.AddMenuItemAsync(new AddMenuItemCommand(Guid.NewGuid(), Guid.NewGuid(), -1));
        await act3.Should().ThrowAsync<InvalidMenuCommandException>();
    }

    [Fact]
    public async Task DeactivatingCatalogProductPreservesMenuItemInCompositionWithInactiveFlagWithoutDeletion()
    {
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand("SEASONAL", "Seasonal Menu"));
        var prod1 = Guid.NewGuid();
        var prod2 = Guid.NewGuid();
        _catalogReader.AddProduct(prod1, "Summer Salad", true);
        _catalogReader.AddProduct(prod2, "Winter Stew", true);

        await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prod1, 0));
        await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prod2, 1));

        // Deactivate prod2 in catalog
        _catalogReader.SetActive(prod2, false);

        var composition = await _menuService.GetMenuCompositionAsync(menu.Id);
        composition.Should().NotBeNull();
        composition.Items.Should().HaveCount(2);

        var item1 = composition.Items.First(i => i.ProductId == prod1);
        item1.ProductName.Should().Be("Summer Salad");
        item1.IsProductActiveInCatalog.Should().BeTrue();

        var item2 = composition.Items.First(i => i.ProductId == prod2);
        item2.ProductName.Should().Be("Winter Stew");
        item2.IsProductActiveInCatalog.Should().BeFalse(); // Preserved historically without deletion!
    }

    [Fact]
    public async Task ReorderMenuItemsUpdatesSequenceAccurately()
    {
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand("REORDER", "Reorder Menu"));
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        _catalogReader.AddProduct(p1, "Soup", true);
        _catalogReader.AddProduct(p2, "Steak", true);
        _catalogReader.AddProduct(p3, "Ice Cream", true);

        var i1 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, p1, 0));
        var i2 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, p2, 1));
        var i3 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, p3, 2));

        // Reorder to 3, 1, 2
        await _menuService.ReorderMenuItemsAsync(new ReorderMenuItemsCommand(menu.Id, new[] { i3.Id, i1.Id, i2.Id }));

        var composition = await _menuService.GetMenuCompositionAsync(menu.Id);
        composition.Items.Select(i => i.MenuItemId).Should().ContainInOrder(i3.Id, i1.Id, i2.Id);
    }
}
