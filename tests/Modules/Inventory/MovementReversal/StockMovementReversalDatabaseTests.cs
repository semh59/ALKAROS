using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.MovementReversal.Tests;

public sealed class MovementReversalTestDb : PgTestDatabase
{
    public MovementReversalTestDb() : base("alkaros_inv_rev_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration059 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql");
        var sql059 = await File.ReadAllTextAsync(migration059);
        await RunAsync(DataSource, sql059);

        var migration060 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql");
        var sql060 = await File.ReadAllTextAsync(migration060);
        await RunAsync(DataSource, sql060);

        var migration061 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "061-stock-balances.up.sql");
        var sql061 = await File.ReadAllTextAsync(migration061);
        await RunAsync(DataSource, sql061);

        var migration062 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "062-stock-reversals.up.sql");
        var sql062 = await File.ReadAllTextAsync(migration062);
        await RunAsync(DataSource, sql062);
    }

    public async Task RollbackMigration062Async()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "062-stock-reversals.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration062Async()
    {
        var migration062 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "062-stock-reversals.up.sql");
        var sql062 = await File.ReadAllTextAsync(migration062);
        await RunAsync(DataSource, sql062);
    }
}

public sealed class StockMovementReversalDatabaseTests : IClassFixture<MovementReversalTestDb>
{
    private readonly MovementReversalTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly PostgresStockBalanceRepository _balanceRepo;
    private readonly StockBalanceProjector _projector;
    private readonly StockMovementReversalService _reversalService;
    private readonly StockMovementService _movementService;
    private readonly StockMasterService _masterService;

    public StockMovementReversalDatabaseTests(MovementReversalTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _movementRepo = new PostgresStockMovementRepository(db.DataSource);
        _balanceRepo = new PostgresStockBalanceRepository(db.DataSource);
        _projector = new StockBalanceProjector(_balanceRepo, _movementRepo, _locationRepo);
        _reversalService = new StockMovementReversalService(_movementRepo, _itemRepo, _locationRepo, _projector, _balanceRepo);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);
        _movementService = new StockMovementService(_movementRepo, _itemRepo, _locationRepo, unitConverter);
    }

    [Fact]
    public async Task ReversalPersistsToDatabaseAndRestoresProjectedOnHandBalance()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen Store", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Ground Beef", StockItemType.RawMaterial, "kg");

        // 1. Initial Receipt (+100 kg)
        var receipt = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 100m, "kg", StockMovementSourceType.PurchaseOrder);
        await _projector.ApplyMovementAsync(receipt);

        var initialBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        initialBalance!.OnHandQuantity.Should().Be(100m);

        // 2. Consumption (-25 kg)
        var consumption = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.Consumption, 25m, "kg", StockMovementSourceType.Order, reason: "Order #501");
        await _projector.ApplyMovementAsync(consumption);

        var balanceAfterConsumption = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balanceAfterConsumption!.OnHandQuantity.Should().Be(75m);

        // 3. Compensating Reversal of the consumption
        var result = await _reversalService.ReverseMovementAsync(
            new StockMovementReversalRequest(consumption.Id, "Order #501 voided before preparation"));

        result.Should().NotBeNull();
        result.ReversalMovement.MovementType.Should().Be(StockMovementType.Reversal);
        result.ReversalMovement.Direction.Should().Be(MovementDirection.In);
        result.ReversalMovement.Quantity.Should().Be(25m);
        result.ReversalMovement.SourceType.Should().Be(StockMovementSourceType.StockMovement);
        result.ReversalMovement.SourceReferenceId.Should().Be(consumption.Id);

        // 4. Verify in DB projection: restored back to 100 kg
        var restoredBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        restoredBalance!.OnHandQuantity.Should().Be(100m);

        // 5. Verify reversal movement row in database
        var loadedReversal = await _movementRepo.GetByIdAsync(result.ReversalMovement.Id);
        loadedReversal.Should().NotBeNull();
        loadedReversal!.SourceReferenceId.Should().Be(consumption.Id);
        loadedReversal.Reason.Should().Be("Order #501 voided before preparation");

        // 6. HasReversal in DB returns true
        var hasReversal = await _movementRepo.HasReversalAsync(consumption.Id);
        hasReversal.Should().BeTrue();
    }

    [Fact]
    public async Task OriginalMovementRowInDatabaseRemainsCompletelyImmutableAfterReversal()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Store", StockLocationType.ColdStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Chicken Breast", StockItemType.RawMaterial, "kg");

        var movement = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.Consumption, 10m, "kg", StockMovementSourceType.Order, reason: "Original consumption");
        await _projector.ApplyMovementAsync(movement);

        var originalSnapshot = await _movementRepo.GetByIdAsync(movement.Id);

        // Reverse
        await _reversalService.ReverseMovementAsync(new StockMovementReversalRequest(movement.Id, "Voided"));

        // Verify original row is byte-identical
        var originalAfter = await _movementRepo.GetByIdAsync(movement.Id);
        originalAfter.Should().NotBeNull();
        originalAfter!.Id.Should().Be(originalSnapshot!.Id);
        originalAfter.StockItemId.Should().Be(originalSnapshot.StockItemId);
        originalAfter.StockLocationId.Should().Be(originalSnapshot.StockLocationId);
        originalAfter.MovementType.Should().Be(originalSnapshot.MovementType);
        originalAfter.Direction.Should().Be(originalSnapshot.Direction);
        originalAfter.Quantity.Should().Be(originalSnapshot.Quantity);
        originalAfter.UnitCode.Should().Be(originalSnapshot.UnitCode);
        originalAfter.SourceType.Should().Be(originalSnapshot.SourceType);
        originalAfter.Reason.Should().Be(originalSnapshot.Reason);
        originalAfter.CreatedAt.Should().Be(originalSnapshot.CreatedAt);
    }

    [Fact]
    public async Task ConcurrentReversalAttemptsOnSameMovementEnforcesSingleReversalWithoutRaces()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Warehouse A", StockLocationType.Warehouse);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Pasta", StockItemType.RawMaterial, "kg");

        var movement = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.Consumption, 15m, "kg", StockMovementSourceType.Order);
        await _projector.ApplyMovementAsync(movement);

        // Concurrently run 10 tasks trying to reverse the exact same movement
        const int concurrentAttempts = 10;
        var results = new List<Task<StockMovementReversalResult>>();

        for (int i = 0; i < concurrentAttempts; i++)
        {
            results.Add(_reversalService.ReverseMovementAsync(
                new StockMovementReversalRequest(movement.Id, $"Concurrent reversal attempt {i}")));
        }

        var successCount = 0;
        var duplicateCount = 0;

        foreach (var task in results)
        {
            try
            {
                await task;
                successCount++;
            }
            catch (DuplicateReversalException)
            {
                duplicateCount++;
            }
        }

        // Exactly one reversal must succeed, and all other 9 must fail with DuplicateReversalException
        successCount.Should().Be(1);
        duplicateCount.Should().Be(concurrentAttempts - 1);

        // Verify in database that exactly 1 reversal exists
        var reversalsInDb = await _movementRepo.GetBySourceAsync(StockMovementSourceType.StockMovement, movement.Id);
        reversalsInDb.Should().ContainSingle(m => m.MovementType == StockMovementType.Reversal);
    }

    [Fact]
    public async Task DeletingStockItemReferencedByReversalMovementThrowsForeignKeyRestriction()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Main Warehouse", StockLocationType.Warehouse);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Sugar", StockItemType.RawMaterial, "kg");

        var movement = await _movementService.RecordMovementAsync(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 50m, "kg", StockMovementSourceType.PurchaseOrder);

        await _reversalService.ReverseMovementAsync(new StockMovementReversalRequest(movement.Id, "Wrong order"));

        var actDelete = () => _itemRepo.DeleteAsync(item.Id);
        await actDelete.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23503" || e.SqlState == "23001");
    }

    [Fact]
    public async Task Migration062RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration062Async();
        await _db.ReapplyMigration062Async();

        // Verify index exists in pg_indexes
        await using var cmd = _db.DataSource.CreateCommand(
            "SELECT count(*) FROM pg_indexes WHERE tablename = 'stock_movements' AND indexname = 'uq_stock_movements_single_reversal';");
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        count.Should().Be(1);
    }
}
