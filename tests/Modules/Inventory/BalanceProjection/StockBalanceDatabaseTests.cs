using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Recipes.Units;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.BalanceProjection.Tests;

public sealed class StockBalanceTestDb : PgTestDatabase
{
    public StockBalanceTestDb() : base("alkaros_inv_bal_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration056 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "056-stock-master.up.sql");
        var sql056 = await File.ReadAllTextAsync(migration056);
        await RunAsync(DataSource, sql056);

        var migration057 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "057-stock-movements.up.sql");
        var sql057 = await File.ReadAllTextAsync(migration057);
        await RunAsync(DataSource, sql057);

        var migration058 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-stock-balances.up.sql");
        var sql058 = await File.ReadAllTextAsync(migration058);
        await RunAsync(DataSource, sql058);
    }

    public async Task RollbackMigration058Async()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-stock-balances.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration058Async()
    {
        var migration058 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-stock-balances.up.sql");
        var sql058 = await File.ReadAllTextAsync(migration058);
        await RunAsync(DataSource, sql058);
    }
}

public sealed class StockBalanceDatabaseTests : IClassFixture<StockBalanceTestDb>
{
    private readonly StockBalanceTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly PostgresStockBalanceRepository _balanceRepo;
    private readonly StockBalanceProjector _projector;
    private readonly StockMasterService _masterService;
    private readonly StockMovementService _movementService;

    public StockBalanceDatabaseTests(StockBalanceTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _movementRepo = new PostgresStockMovementRepository(db.DataSource);
        _balanceRepo = new PostgresStockBalanceRepository(db.DataSource);
        _projector = new StockBalanceProjector(_balanceRepo, _movementRepo, _locationRepo);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);
        _movementService = new StockMovementService(_movementRepo, _itemRepo, _locationRepo, unitConverter);
    }

    [Fact]
    public async Task ApplyOnHandDeltaCreatesAndUpdatesBalancesAccurately()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen Store", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Olive Oil", StockItemType.RawMaterial, "l");

        // 1. First delta (+50)
        var b1 = await _balanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 50m);
        b1.OnHandQuantity.Should().Be(50m);
        b1.RowVersion.Should().Be(1);

        // 2. Second delta (-12.5)
        var b2 = await _balanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, -12.5m);
        b2.OnHandQuantity.Should().Be(37.5m);
        b2.RowVersion.Should().Be(2);

        // 3. Retrieve
        var loaded = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        loaded.Should().NotBeNull();
        loaded!.OnHandQuantity.Should().Be(37.5m);
        loaded.RowVersion.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentWritersApplyingDeltasProduceExactNetSumWithoutLostUpdates()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Warehouse 1", StockLocationType.Warehouse);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Rice", StockItemType.RawMaterial, "kg");

        // Concurrently run 10 tasks, each applying +2.5m
        const int taskCount = 10;
        const decimal deltaPerTask = 2.5m;
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => _balanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, deltaPerTask))
            .ToArray();

        await Task.WhenAll(tasks);

        var finalBalance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        finalBalance.Should().NotBeNull();
        // 10 * 2.5m = 25m
        finalBalance!.OnHandQuantity.Should().Be(taskCount * deltaPerTask);
        finalBalance.RowVersion.Should().Be(taskCount);
    }

    [Fact]
    public async Task FullLedgerRebuildFromDatabaseMovementsReconcilesAccurately()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Store", StockLocationType.ColdStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Butter", StockItemType.RawMaterial, "kg");

        // Record movements in ledger
        var m1 = await _movementService.RecordMovementAsync(item.Id, loc.Id, StockMovementType.PurchaseReceipt, 100m, "kg", StockMovementSourceType.PurchaseOrder);
        var m2 = await _movementService.RecordMovementAsync(item.Id, loc.Id, StockMovementType.Consumption, 25m, "kg", StockMovementSourceType.Order);
        var m3 = await _movementService.RecordMovementAsync(item.Id, loc.Id, StockMovementType.Waste, 5m, "kg", StockMovementSourceType.WasteRecord);
        await _movementService.ReverseMovementAsync(m3.Id, "Waste cancelled"); // Reversal cancels waste (+5)

        // Project balances naturally
        await _projector.ApplyMovementAsync(m1);
        await _projector.ApplyMovementAsync(m2);

        // Artificially corrupt the projected balance to test rebuild reconciliation
        await _balanceRepo.SetExactBalanceAsync(item.Id, loc.Id, 99999m);
        var corrupted = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        corrupted!.OnHandQuantity.Should().Be(99999m);

        // Run rebuild
        var report = await _projector.RebuildAllBalancesAsync();
        report.TotalMovementsProcessed.Should().BeGreaterThan(0);

        // Recomputed balance should be: 100 - 25 - 5 + 5 = 75m
        var reconciled = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        reconciled.Should().NotBeNull();
        reconciled!.OnHandQuantity.Should().Be(75m);

        // Replay single item/location check
        var replayed = await _projector.ReplayBalanceForItemAndLocationAsync(item.Id, loc.Id);
        replayed.Should().Be(75m);
    }

    [Fact]
    public async Task DeletingStockItemReferencedByStockBalanceThrowsForeignKeyRestriction()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Dry Storage", StockLocationType.DryStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Salt", StockItemType.RawMaterial, "kg");

        await _balanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 10m);

        var actDelete = () => _itemRepo.DeleteAsync(item.Id);
        await actDelete.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23503" || e.SqlState == "23001");
    }

    [Fact]
    public async Task Migration058RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration058Async();
        await _db.ReapplyMigration058Async();

        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM inventory.stock_balances;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
