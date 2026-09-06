using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.MovementLedger.Tests;

public sealed class StockMovementTestDb : PgTestDatabase
{
    public StockMovementTestDb() : base("alkaros_inv_mvt_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration059 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql");
        var sql059 = await File.ReadAllTextAsync(migration059);
        await RunAsync(DataSource, sql059);

        var migration060 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql");
        var sql060 = await File.ReadAllTextAsync(migration060);
        await RunAsync(DataSource, sql060);
    }

    public async Task RollbackMigration060Async()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration060Async()
    {
        var migration060 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql");
        var sql060 = await File.ReadAllTextAsync(migration060);
        await RunAsync(DataSource, sql060);
    }
}

public sealed class StockMovementDatabaseTests : IClassFixture<StockMovementTestDb>
{
    private readonly StockMovementTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresProductStockMappingRepository _mappingRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly StockMasterService _masterService;
    private readonly StockMovementService _movementService;

    public StockMovementDatabaseTests(StockMovementTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _movementRepo = new PostgresStockMovementRepository(db.DataSource);
        var unitConverter = new UnitConverter();
        _masterService = new StockMasterService(_locationRepo, _itemRepo, _mappingRepo, unitConverter);
        _movementService = new StockMovementService(_movementRepo, _itemRepo, _locationRepo, unitConverter);
    }

    [Fact]
    public async Task AppendMovementAndRetrievePersistsAccurately()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen Store", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Cheese", StockItemType.RawMaterial, "kg");

        var sourceRef = Guid.NewGuid();
        var movement = await _movementService.RecordMovementAsync(
            stockItemId: item.Id,
            stockLocationId: loc.Id,
            movementType: StockMovementType.PurchaseReceipt,
            quantity: 25.75m,
            unitCode: "kg",
            sourceType: StockMovementSourceType.PurchaseOrder,
            sourceReferenceId: sourceRef,
            reason: "Initial purchase delivery");

        var loaded = await _movementRepo.GetByIdAsync(movement.Id);
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(movement.Id);
        loaded.StockItemId.Should().Be(item.Id);
        loaded.StockLocationId.Should().Be(loc.Id);
        loaded.MovementType.Should().Be(StockMovementType.PurchaseReceipt);
        loaded.Direction.Should().Be(MovementDirection.In);
        loaded.Quantity.Should().Be(25.75m);
        loaded.UnitCode.Should().Be("kg");
        loaded.SourceType.Should().Be(StockMovementSourceType.PurchaseOrder);
        loaded.SourceReferenceId.Should().Be(sourceRef);
        loaded.Reason.Should().Be("Initial purchase delivery");
        loaded.Effect.OnHandDelta.Should().Be(25.75m);

        var byItem = await _movementRepo.GetByStockItemAsync(item.Id);
        byItem.Should().ContainSingle(m => m.Id == movement.Id);

        var byLoc = await _movementRepo.GetByLocationAsync(loc.Id);
        byLoc.Should().ContainSingle(m => m.Id == movement.Id);

        var bySource = await _movementRepo.GetBySourceAsync(StockMovementSourceType.PurchaseOrder, sourceRef);
        bySource.Should().ContainSingle(m => m.Id == movement.Id);
    }

    [Fact]
    public async Task AttemptingToUpdateStockMovementThrowsImmutabilityException()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Main Warehouse", StockLocationType.Warehouse);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Flour", StockItemType.RawMaterial, "kg");

        var movement = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 50m, "kg", StockMovementSourceType.PurchaseOrder);

        // Attempting direct raw SQL UPDATE
        var actUpdate = async () =>
        {
            await using var cmd = _db.DataSource.CreateCommand(
                "UPDATE inventory.stock_movements SET quantity = 999 WHERE stock_movement_id = $1;");
            cmd.Parameters.AddWithValue(movement.Id);
            await cmd.ExecuteNonQueryAsync();
        };

        await actUpdate.Should().ThrowAsync<PostgresException>()
            .WithMessage("*immutable and cannot be updated or deleted*");
    }

    [Fact]
    public async Task AttemptingToDeleteStockMovementThrowsImmutabilityException()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Bar Store", StockLocationType.Bar);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Soda", StockItemType.Packaging, "piece");

        var movement = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 100m, "piece", StockMovementSourceType.PurchaseOrder);

        // Attempting direct raw SQL DELETE
        var actDelete = async () =>
        {
            await using var cmd = _db.DataSource.CreateCommand(
                "DELETE FROM inventory.stock_movements WHERE stock_movement_id = $1;");
            cmd.Parameters.AddWithValue(movement.Id);
            await cmd.ExecuteNonQueryAsync();
        };

        await actDelete.Should().ThrowAsync<PostgresException>()
            .WithMessage("*immutable and cannot be updated or deleted*");
    }

    [Fact]
    public async Task ReversalFlowInDatabaseWorksCleanlyAndBlocksDoubleReversal()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Store", StockLocationType.ColdStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Milk", StockItemType.RawMaterial, "l");

        var consumption = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.Consumption, 12m, "l", StockMovementSourceType.Order, reason: "Order item");

        consumption.Direction.Should().Be(MovementDirection.Out);

        // Reversal
        var reversal = await _movementService.ReverseMovementAsync(consumption.Id, "Voided order");
        reversal.MovementType.Should().Be(StockMovementType.Reversal);
        reversal.Direction.Should().Be(MovementDirection.In);
        reversal.Quantity.Should().Be(12m);
        reversal.SourceReferenceId.Should().Be(consumption.Id);
        reversal.SourceType.Should().Be(StockMovementSourceType.StockMovement);

        // Verify HasReversalAsync
        var hasReversal = await _movementRepo.HasReversalAsync(consumption.Id);
        hasReversal.Should().BeTrue();

        // Attempting second reversal must fail
        var actDouble = () => _movementService.ReverseMovementAsync(consumption.Id, "Duplicate void attempt");
        await actDouble.Should().ThrowAsync<DuplicateReversalException>();
    }

    [Fact]
    public async Task DeletingStockItemReferencedByStockMovementThrowsForeignKeyRestriction()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Dry Store", StockLocationType.DryStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Salt", StockItemType.RawMaterial, "kg");

        await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 10m, "kg", StockMovementSourceType.PurchaseOrder);

        var actDelete = () => _itemRepo.DeleteAsync(item.Id);
        await actDelete.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23503" || e.SqlState == "23001");
    }

    [Fact]
    public async Task Migration060RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration060Async();
        await _db.ReapplyMigration060Async();

        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM inventory.stock_movements;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
