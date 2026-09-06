using System.Globalization;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Menu.DailyMenuLifecycle.Tests;

public sealed class DailyMenuLifecycleTestDb : PgTestDatabase
{
    public DailyMenuLifecycleTestDb() : base("alkaros_daily_menu_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var catalogUp = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "006-catalog.up.sql");
        var sqlCatalog = await File.ReadAllTextAsync(catalogUp);
        await RunAsync(DataSource, sqlCatalog);

        const string recipeSchemaSql = "CREATE SCHEMA IF NOT EXISTS recipe;";
        await RunAsync(DataSource, recipeSchemaSql);

        var recipeUp = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.up.sql");
        var sqlRecipe = await File.ReadAllTextAsync(recipeUp);
        await RunAsync(DataSource, sqlRecipe);

        var dailyMenuUp = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.up.sql");
        var sqlDailyMenu = await File.ReadAllTextAsync(dailyMenuUp);
        await RunAsync(DataSource, sqlDailyMenu);
    }
}

public sealed class DailyMenuLifecycleDatabaseTests : IClassFixture<DailyMenuLifecycleTestDb>
{
    private readonly DailyMenuLifecycleTestDb _db;
    private readonly PostgresDailyMenuRepository _menuRepo;
    private readonly PostgresDailyMenuItemRepository _itemRepo;
    private readonly PostgresDailyMenuItemHistoryRepository _historyRepo;
    private readonly PostgresCatalogProductPriceReader _catalogReader;
    private readonly PostgresRecipeVersionValidator _recipeValidator;
    private readonly BusinessDateProvider _dateProvider;
    private readonly DailyMenuService _service;

    public DailyMenuLifecycleDatabaseTests(DailyMenuLifecycleTestDb db)
    {
        _db = db;
        _menuRepo = new PostgresDailyMenuRepository(db.DataSource);
        _itemRepo = new PostgresDailyMenuItemRepository(db.DataSource);
        _historyRepo = new PostgresDailyMenuItemHistoryRepository(db.DataSource);
        _catalogReader = new PostgresCatalogProductPriceReader(db.DataSource);
        _recipeValidator = new PostgresRecipeVersionValidator(db.DataSource);
        _dateProvider = new BusinessDateProvider();

        _service = new DailyMenuService(
            _menuRepo, _itemRepo, _historyRepo, _catalogReader, _recipeValidator, _dateProvider);
    }

    [Fact]
    public async Task FullPersistenceLifecycleAndModificationsWithHistoryLogging()
    {
        var testDate = new DateOnly(2027, 1, 1).AddDays(Random.Shared.Next(1, 1000));
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(testDate, "Winter Feast"));

        var prodId = await InsertCatalogProductAsync("Urfa Kebap", 280m);
        var item = await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(
            DailyMenuId: menu.Id,
            ProductId: prodId,
            PlannedPortions: 40m));

        item.ProductNameSnapshot.Should().Be("Urfa Kebap");
        item.Price.Should().Be(280m);

        // Open menu
        await _service.OpenDailyMenuAsync(new OpenDailyMenuCommand(menu.Id));

        // Update price
        var staffId = Guid.NewGuid();
        await _service.UpdateDailyMenuItemPriceAsync(new UpdateDailyMenuItemPriceCommand(item.Id, 300m, staffId));

        var updatedItem = await _itemRepo.GetByIdAsync(item.Id);
        updatedItem!.Price.Should().Be(300m);

        var history = await _historyRepo.GetByDailyMenuItemIdAsync(item.Id);
        history.Should().HaveCount(1);
        history[0].ChangedBy.Should().Be(staffId);

        // Close menu
        await _service.CloseDailyMenuAsync(new CloseDailyMenuCommand(menu.Id, staffId));

        var closedMenu = await _menuRepo.GetByIdAsync(menu.Id);
        closedMenu!.Status.Should().Be(DailyMenuStatus.Closed);
        closedMenu.ClosedBy.Should().Be(staffId);

        // Verify closed menu rejects any further modifications
        var actAdd = () => _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));
        await actAdd.Should().ThrowAsync<DailyMenuClosedException>();
    }

    [Fact]
    public async Task DatabaseUniqueConstraintRejectsDuplicateServiceDate()
    {
        var testDate = new DateOnly(2028, 5, 1).AddDays(Random.Shared.Next(1, 1000));
        await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(testDate));

        var act = () => _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(testDate));
        await act.Should().ThrowAsync<DuplicateDailyMenuBusinessDateException>();
    }

    [Fact]
    public async Task DatabaseUniqueConstraintRejectsDuplicateProductInSameDailyMenu()
    {
        var testDate = new DateOnly(2029, 6, 1).AddDays(Random.Shared.Next(1, 1000));
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(testDate));
        var prodId = await InsertCatalogProductAsync("Lahmacun", 90m);

        await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));

        var act = () => _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));
        await act.Should().ThrowAsync<DuplicateDailyMenuItemException>();
    }

    [Fact]
    public async Task DownMigrationDropsDailyMenuTablesCleanly()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await using (var cmd = _db.DataSource.CreateCommand(downSql))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        const string checkSql = @"
            SELECT COUNT(*) FROM information_schema.tables 
            WHERE table_schema = 'menu' AND table_name IN ('daily_menus', 'daily_menu_items', 'daily_menu_item_history');";
        await using (var checkCmd = _db.DataSource.CreateCommand(checkSql))
        {
            var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            count.Should().Be(0);
        }

        // Re-apply up SQL
        var upSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.up.sql");
        var upSql = await File.ReadAllTextAsync(upSqlPath);
        await using (var upCmd = _db.DataSource.CreateCommand(upSql))
        {
            await upCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<Guid> InsertCatalogProductAsync(string name, decimal price)
    {
        var id = Guid.NewGuid();
        var sku = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        const string sql = @"
            INSERT INTO catalog.products (product_id, sku, name, active, product_type, stock_mode, current_price)
            VALUES ($1, $2, $3, true, 1, 1, $4);";

        await using var cmd = _db.DataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(sku);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(price);
        await cmd.ExecuteNonQueryAsync();

        return id;
    }
}
