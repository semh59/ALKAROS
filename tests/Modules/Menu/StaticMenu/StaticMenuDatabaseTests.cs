using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Menu.StaticMenu.Tests;

public sealed class StaticMenuTestDb : PgTestDatabase
{
    public StaticMenuTestDb() : base("alkaros_static_menu_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var catalogUp = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "006-catalog.up.sql");
        var sqlCatalog = await File.ReadAllTextAsync(catalogUp);
        await RunAsync(DataSource, sqlCatalog);

        var menuUp = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "063-static-menu.up.sql");
        var sqlMenu = await File.ReadAllTextAsync(menuUp);
        await RunAsync(DataSource, sqlMenu);
    }
}

public sealed class StaticMenuDatabaseTests : IClassFixture<StaticMenuTestDb>
{
    private readonly StaticMenuTestDb _db;
    private readonly PostgresMenuRepository _menuRepo;
    private readonly PostgresMenuItemRepository _menuItemRepo;
    private readonly PostgresCatalogProductReader _catalogReader;
    private readonly StaticMenuService _menuService;

    public StaticMenuDatabaseTests(StaticMenuTestDb db)
    {
        _db = db;
        _menuRepo = new PostgresMenuRepository(db.DataSource);
        _menuItemRepo = new PostgresMenuItemRepository(db.DataSource);
        _catalogReader = new PostgresCatalogProductReader(db.DataSource);
        _menuService = new StaticMenuService(_menuRepo, _menuItemRepo, _catalogReader);
    }

    [Fact]
    public async Task FullPersistenceMenuAndItemsFlowVerifiesStorageAndQueries()
    {
        var code = "MNU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand(code, "A La Carte Menu"));

        var prodId1 = await InsertCatalogProductAsync("Product Alpha", true);
        var prodId2 = await InsertCatalogProductAsync("Product Beta", true);

        var item1 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId1, 1));
        var item2 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId2, 2));

        var comp = await _menuService.GetMenuCompositionAsync(menu.Id);
        comp.Menu.Id.Should().Be(menu.Id);
        comp.Menu.Code.Should().Be(code);
        comp.Items.Should().HaveCount(2);
        comp.Items[0].ProductId.Should().Be(prodId1);
        comp.Items[0].ProductName.Should().Be("Product Alpha");
        comp.Items[0].IsProductActiveInCatalog.Should().BeTrue();
        comp.Items[1].ProductId.Should().Be(prodId2);
        comp.Items[1].ProductName.Should().Be("Product Beta");
    }

    [Fact]
    public async Task DatabaseUniqueConstraintRejectsDuplicateProductInSameMenu()
    {
        var code = "MNU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand(code, "Duplicate Test Menu"));
        var prodId = await InsertCatalogProductAsync("Unique Item", true);

        await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId, 0));

        var act = () => _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, prodId, 1));
        await act.Should().ThrowAsync<DuplicateMenuItemProductException>();
    }

    [Fact]
    public async Task DatabaseUniqueConstraintRejectsDuplicateMenuCode()
    {
        var code = "MNU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await _menuService.CreateMenuAsync(new CreateMenuCommand(code, "Code Test 1"));

        var act = () => _menuService.CreateMenuAsync(new CreateMenuCommand(code, "Code Test 2"));
        await act.Should().ThrowAsync<DuplicateMenuCodeException>();
    }

    [Fact]
    public async Task CatalogProductDeactivationPreservesMenuItemInCompositionWithInactiveFlag()
    {
        var code = "MNU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand(code, "Deactivation Test Menu"));

        var activeProd = await InsertCatalogProductAsync("Active Dish", true);
        var toDeactivateProd = await InsertCatalogProductAsync("Discontinued Dish", true);

        await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, activeProd, 0));
        await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, toDeactivateProd, 1));

        // Deactivate product in catalog table directly
        await DeactivateCatalogProductAsync(toDeactivateProd);

        var comp = await _menuService.GetMenuCompositionAsync(menu.Id);
        comp.Items.Should().HaveCount(2);

        var deactivatedItem = comp.Items.First(i => i.ProductId == toDeactivateProd);
        deactivatedItem.ProductName.Should().Be("Discontinued Dish");
        deactivatedItem.IsProductActiveInCatalog.Should().BeFalse(); // Clearly displayed as inactive catalog item
        deactivatedItem.IsActive.Should().BeTrue(); // Menu item itself is not silently deleted
    }

    [Fact]
    public async Task ReorderMenuItemsPersistsCorrectSequenceInDatabase()
    {
        var code = "MNU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var menu = await _menuService.CreateMenuAsync(new CreateMenuCommand(code, "Reorder DB Menu"));

        var p1 = await InsertCatalogProductAsync("Course 1", true);
        var p2 = await InsertCatalogProductAsync("Course 2", true);
        var p3 = await InsertCatalogProductAsync("Course 3", true);

        var i1 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, p1, 0));
        var i2 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, p2, 1));
        var i3 = await _menuService.AddMenuItemAsync(new AddMenuItemCommand(menu.Id, p3, 2));

        await _menuService.ReorderMenuItemsAsync(new ReorderMenuItemsCommand(menu.Id, new[] { i2.Id, i3.Id, i1.Id }));

        var comp = await _menuService.GetMenuCompositionAsync(menu.Id);
        comp.Items.Select(i => i.MenuItemId).Should().ContainInOrder(i2.Id, i3.Id, i1.Id);
    }

    [Fact]
    public async Task DownMigrationDropsStaticMenuTablesCleanly()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "063-static-menu.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await using (var cmd = _db.DataSource.CreateCommand(downSql))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        const string checkSql = @"
            SELECT COUNT(*) FROM information_schema.tables 
            WHERE table_schema = 'menu' AND table_name IN ('menus', 'menu_items');";
        await using (var checkCmd = _db.DataSource.CreateCommand(checkSql))
        {
            var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            count.Should().Be(0);
        }

        // Re-apply up SQL so other tests/fixtures remain functional
        var upSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "063-static-menu.up.sql");
        var upSql = await File.ReadAllTextAsync(upSqlPath);
        await using (var upCmd = _db.DataSource.CreateCommand(upSql))
        {
            await upCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<Guid> InsertCatalogProductAsync(string name, bool active)
    {
        var id = Guid.NewGuid();
        var sku = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        const string sql = @"
            INSERT INTO catalog.products (product_id, sku, name, active, product_type, stock_mode)
            VALUES ($1, $2, $3, $4, 1, 1);";

        await using var cmd = _db.DataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(sku);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(active);
        await cmd.ExecuteNonQueryAsync();

        return id;
    }

    private async Task DeactivateCatalogProductAsync(Guid productId)
    {
        const string sql = "UPDATE catalog.products SET active = false WHERE product_id = $1;";
        await using var cmd = _db.DataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);
        await cmd.ExecuteNonQueryAsync();
    }
}
