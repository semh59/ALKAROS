using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.ModifierStock;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PhysicalCounts.Tests;

public sealed class PhysicalCountTestDb : PgTestDatabase
{
    public PhysicalCountTestDb() : base("alkaros_inv008_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
        {
            "059-stock-master.up.sql",
            "060-stock-movements.up.sql",
            "061-stock-balances.up.sql",
            "087-inventory-stock-balances-non-negative.up.sql",
            "117-stock-physical-counts.up.sql",
        })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file);
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
        }
    }

    public async Task RollbackMigration117Async()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "117-stock-physical-counts.down.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ReapplyMigration117Async()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "117-stock-physical-counts.up.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }
}

public sealed class PhysicalCountDatabaseTests : IClassFixture<PhysicalCountTestDb>
{
    private readonly PhysicalCountTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly PostgresStockBalanceRepository _balanceRepo;
    private readonly PostgresPhysicalCountRepository _countRepo;
    private readonly StockMasterService _masterService;
    private readonly PhysicalCountService _service;

    public PhysicalCountDatabaseTests(PhysicalCountTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _movementRepo = new PostgresStockMovementRepository(db.DataSource);
        _balanceRepo = new PostgresStockBalanceRepository(db.DataSource);
        _countRepo = new PostgresPhysicalCountRepository(db.DataSource);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);
        var transactionRunner = new PostgresInventoryTransactionRunner(db.DataSource);
        _service = new PhysicalCountService(transactionRunner, _itemRepo, _locationRepo, _balanceRepo, _movementRepo, _countRepo);
    }

    private async Task<(StockLocation Location, StockItem Item)> SeedItemAsync(string name, string unit = "kg")
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Depo " + name, StockLocationType.Warehouse);
        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, name, StockItemType.RawMaterial, unit);
        return (loc, item);
    }

    [Fact]
    public async Task FirstCountOnAZeroBalanceRecordsAnIncreaseMovement()
    {
        var (loc, item) = await SeedItemAsync("Un");
        var userId = Guid.NewGuid();

        var result = await _service.RecordPhysicalCountAsync(
            new PhysicalCountRequest(item.Id, loc.Id, 25m, userId, "İlk sayım"));

        result.PreviousOnHandQuantity.Should().Be(0m);
        result.NewOnHandQuantity.Should().Be(25m);
        result.Count.Delta.Should().Be(25m);
        result.Count.ResultingMovementId.Should().NotBeNull();

        var balance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balance!.OnHandQuantity.Should().Be(25m);

        var movements = await _movementRepo.GetByStockItemAsync(item.Id);
        movements.Should().ContainSingle(m =>
            m.MovementType == StockMovementType.Adjustment
            && m.SourceType == StockMovementSourceType.InventoryAudit
            && m.CreatedBy == userId);
    }

    [Fact]
    public async Task ACountMatchingTheCurrentBalanceRecordsNoMovementButStillPersistsTheCount()
    {
        var (loc, item) = await SeedItemAsync("Şeker");
        var userId = Guid.NewGuid();

        await _service.RecordPhysicalCountAsync(new PhysicalCountRequest(item.Id, loc.Id, 10m, userId, null));
        var movementsAfterFirst = await _movementRepo.GetByStockItemAsync(item.Id);

        var secondResult = await _service.RecordPhysicalCountAsync(
            new PhysicalCountRequest(item.Id, loc.Id, 10m, userId, "Doğrulama sayımı"));

        secondResult.PreviousOnHandQuantity.Should().Be(10m);
        secondResult.NewOnHandQuantity.Should().Be(10m);
        secondResult.Count.Delta.Should().Be(0m);
        secondResult.Count.ResultingMovementId.Should().BeNull();

        var movementsAfterSecond = await _movementRepo.GetByStockItemAsync(item.Id);
        movementsAfterSecond.Should().HaveCount(movementsAfterFirst.Count);

        var lastCount = await _countRepo.GetMostRecentBeforeAsync(item.Id, loc.Id, DateTimeOffset.UtcNow.AddMinutes(1));
        lastCount.Should().NotBeNull();
        lastCount!.CountedQuantity.Should().Be(10m);
        lastCount.ResultingMovementId.Should().BeNull();
    }

    [Fact]
    public async Task ACountLowerThanBalanceProducesADecreaseMovement()
    {
        var (loc, item) = await SeedItemAsync("Zeytinyağı", "l");
        var userId = Guid.NewGuid();

        await _service.RecordPhysicalCountAsync(new PhysicalCountRequest(item.Id, loc.Id, 20m, userId, null));
        var result = await _service.RecordPhysicalCountAsync(
            new PhysicalCountRequest(item.Id, loc.Id, 14m, userId, "Fire tespit edildi"));

        result.PreviousOnHandQuantity.Should().Be(20m);
        result.NewOnHandQuantity.Should().Be(14m);
        result.Count.Delta.Should().Be(-6m);

        var balance = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balance!.OnHandQuantity.Should().Be(14m);
    }

    [Fact]
    public async Task GetMostRecentBeforeReturnsTheLatestCountAtOrBeforeTheGivenTime()
    {
        var (loc, item) = await SeedItemAsync("Tuz");
        var userId = Guid.NewGuid();

        await _service.RecordPhysicalCountAsync(new PhysicalCountRequest(item.Id, loc.Id, 5m, userId, "İlk"));
        var before = DateTimeOffset.UtcNow;
        await Task.Delay(50);
        await _service.RecordPhysicalCountAsync(new PhysicalCountRequest(item.Id, loc.Id, 8m, userId, "İkinci"));

        var atBefore = await _countRepo.GetMostRecentBeforeAsync(item.Id, loc.Id, before);
        atBefore.Should().NotBeNull();
        atBefore!.CountedQuantity.Should().Be(5m);

        var atNow = await _countRepo.GetMostRecentBeforeAsync(item.Id, loc.Id, DateTimeOffset.UtcNow.AddMinutes(1));
        atNow.Should().NotBeNull();
        atNow!.CountedQuantity.Should().Be(8m);
    }

    [Fact]
    public async Task Migration117RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration117Async();
        await _db.ReapplyMigration117Async();

        await using var cmd = _db.DataSource.CreateCommand("SELECT count(*) FROM inventory.stock_physical_counts;");
        var count = await cmd.ExecuteScalarAsync();
        count.Should().NotBeNull();
    }
}
