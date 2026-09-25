using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.ManualAdjustments.Tests;

public sealed class ManualAdjustmentTestDb : PgTestDatabase
{
    public ManualAdjustmentTestDb() : base("alkaros_inv_adj_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration059 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql");
        var sql059 = await File.ReadAllTextAsync(migration059);
        await RunAsync(DataSource, sql059);

        var migration118 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "118-stock-items-reorder-point.up.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(migration118));

        var migration060 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql");
        var sql060 = await File.ReadAllTextAsync(migration060);
        await RunAsync(DataSource, sql060);

        var migration061 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "061-stock-balances.up.sql");
        var sql061 = await File.ReadAllTextAsync(migration061);
        await RunAsync(DataSource, sql061);

        var migration062 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "062-stock-reversals.up.sql");
        var sql062 = await File.ReadAllTextAsync(migration062);
        await RunAsync(DataSource, sql062);

        var migration087 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "087-inventory-stock-balances-non-negative.up.sql");
        var sql087 = await File.ReadAllTextAsync(migration087);
        await RunAsync(DataSource, sql087);
    }
}

public sealed class ManualAdjustmentDatabaseTests : IClassFixture<ManualAdjustmentTestDb>
{
    private readonly ManualAdjustmentTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly PostgresStockBalanceRepository _balanceRepo;
    private readonly StockBalanceProjector _projector;
    private readonly InventoryAdjustmentService _adjustmentService;
    private readonly StockMasterService _masterService;

    public ManualAdjustmentDatabaseTests(ManualAdjustmentTestDb db)
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
        var transactionRunner = new PostgresInventoryTransactionRunner(db.DataSource);
        _adjustmentService = new InventoryAdjustmentService(
            transactionRunner, _movementRepo, _itemRepo, _locationRepo, _balanceRepo, unitConverter);
    }

    [Fact]
    public async Task FullAdjustmentPersistenceFlowAppliesDeltasAndPreservesAuditTrail()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Bar 1", StockLocationType.Bar);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Gin", StockItemType.RawMaterial, "l");

        var managerId = Guid.NewGuid();

        // 1. Initial manual adjustment increase (+10l)
        var incResult = await _adjustmentService.AdjustInventoryAsync(new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Increase,
            Quantity: 10m,
            UnitCode: "l",
            Reason: "Initial inventory setup",
            AuthorizedBy: managerId));

        incResult.PreviousOnHandQuantity.Should().Be(0m);
        incResult.NewOnHandQuantity.Should().Be(10m);

        var bal1 = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        bal1!.OnHandQuantity.Should().Be(10m);

        // 2. Manual adjustment decrease (-3l)
        var decResult = await _adjustmentService.AdjustInventoryAsync(new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Decrease,
            Quantity: 3m,
            UnitCode: "l",
            Reason: "Broken bottle during audit",
            AuthorizedBy: managerId));

        decResult.PreviousOnHandQuantity.Should().Be(10m);
        decResult.NewOnHandQuantity.Should().Be(7m);

        var bal2 = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        bal2!.OnHandQuantity.Should().Be(7m);

        // 3. Verify movements in database
        var movements = await _movementRepo.GetByStockItemAsync(item.Id);
        movements.Should().HaveCount(2);
        movements.All(m => m.MovementType == StockMovementType.Adjustment).Should().BeTrue();
        movements.All(m => m.SourceType == StockMovementSourceType.InventoryAudit).Should().BeTrue();
        movements.All(m => m.CreatedBy == managerId).Should().BeTrue();

        // 4. Ledger replay consistency
        var replayedBalance = await _projector.ReplayBalanceForItemAndLocationAsync(item.Id, loc.Id);
        replayedBalance.Should().Be(7m);
    }

    [Fact]
    public async Task AdjustmentFailingNegativeConstraintDoesNotPersistAnyMovement()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen 1", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Butter", StockItemType.RawMaterial, "kg");

        // Current balance is 0
        var actDecreaseFromZero = () => _adjustmentService.AdjustInventoryAsync(new InventoryAdjustmentRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Direction: AdjustmentDirection.Decrease,
            Quantity: 1m,
            UnitCode: "kg",
            Reason: "Missing item",
            AuthorizedBy: Guid.NewGuid()));

        await actDecreaseFromZero.Should().ThrowAsync<NegativeInventoryResultException>();

        // Verify zero movements were persisted
        var movements = await _movementRepo.GetByStockItemAsync(item.Id);
        movements.Should().BeEmpty();
    }
}
