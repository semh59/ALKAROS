using ALKAROS.Recipes.Units;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.StockMaster.Tests;

public sealed class StockMasterTestDb : PgTestDatabase
{
    public StockMasterTestDb() : base("alkaros_inv_stock_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration059 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql");
        var sql059 = await File.ReadAllTextAsync(migration059);
        await RunAsync(DataSource, sql059);
    }

    public async Task RollbackMigration059Async()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration059Async()
    {
        var migration059 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql");
        var sql059 = await File.ReadAllTextAsync(migration059);
        await RunAsync(DataSource, sql059);
    }
}

public sealed class StockMasterDatabaseTests : IClassFixture<StockMasterTestDb>
{
    private readonly StockMasterTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresProductStockMappingRepository _mappingRepo;
    private readonly StockMasterService _service;

    public StockMasterDatabaseTests(StockMasterTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _service = new StockMasterService(_locationRepo, _itemRepo, _mappingRepo, new UnitConverter());
    }

    [Fact]
    public async Task CreateLocationAndRetrievePersistsCorrectly()
    {
        var code = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _service.CreateLocationAsync(code, "Cold Room 1", StockLocationType.ColdStorage);

        var loaded = await _locationRepo.GetByIdAsync(loc.Id);
        loaded.Should().NotBeNull();
        loaded!.Code.Should().Be(code);
        loaded.Name.Should().Be("Cold Room 1");
        loaded.LocationType.Should().Be(StockLocationType.ColdStorage);
        loaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateLocationCodeThrowsDuplicateStockLocationException()
    {
        var code = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await _service.CreateLocationAsync(code, "Warehouse A", StockLocationType.Warehouse);

        var act = () => _service.CreateLocationAsync(code, "Warehouse Duplicate", StockLocationType.Warehouse);
        await act.Should().ThrowAsync<DuplicateStockLocationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateStockItemWithDefaultLocationPersistsAndLoadsCorrectly()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _service.CreateLocationAsync(locCode, "Kitchen Store", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _service.CreateStockItemAsync(
            code: itemCode,
            name: "Tomato Paste",
            itemType: StockItemType.RawMaterial,
            trackingUnitCode: "kg",
            defaultLocationId: loc.Id);

        var loaded = await _itemRepo.GetByIdAsync(item.Id);
        loaded.Should().NotBeNull();
        loaded!.Code.Should().Be(itemCode);
        loaded.Name.Should().Be("Tomato Paste");
        loaded.ItemType.Should().Be(StockItemType.RawMaterial);
        loaded.TrackingUnitCode.Should().Be("kg");
        loaded.DefaultLocationId.Should().Be(loc.Id);
        loaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMovementTargetRejectsInactiveItemOrLocation()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _service.CreateLocationAsync(locCode, "Test Loc", StockLocationType.Warehouse, isActive: true);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _service.CreateStockItemAsync(itemCode, "Item 1", StockItemType.Packaging, "piece", isActive: false);

        var act = () => _service.ValidateMovementTargetAsync(item.Id, loc.Id);
        await act.Should().ThrowAsync<InactiveStockItemException>();
    }

    [Fact]
    public async Task ProductStockMappingPersistsAndLoadsCorrectly()
    {
        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _service.CreateStockItemAsync(itemCode, "Burger Bun", StockItemType.RawMaterial, "piece");

        var productId = Guid.NewGuid();
        var mapping = await _service.AssignProductToStockItemAsync(productId, item.Id, quantityMultiplier: 1.0m, notes: "Single bun");

        var loaded = await _mappingRepo.GetByProductIdAsync(productId);
        loaded.Should().HaveCount(1);
        loaded[0].StockItemId.Should().Be(item.Id);
        loaded[0].QuantityMultiplier.Should().Be(1.0m);
        loaded[0].Notes.Should().Be("Single bun");
    }

    [Fact]
    public async Task DeletingStockItemReferencedByProductMappingThrowsForeignKeyRestriction()
    {
        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _service.CreateStockItemAsync(itemCode, "Patty", StockItemType.Portion, "portion");

        var productId = Guid.NewGuid();
        await _service.AssignProductToStockItemAsync(productId, item.Id, quantityMultiplier: 1.0m);

        var act = () => _itemRepo.DeleteAsync(item.Id);
        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23503" || e.SqlState == "23001");
    }

    [Fact]
    public async Task Migration059RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration059Async();
        await _db.ReapplyMigration059Async();

        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM inventory.stock_items;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
