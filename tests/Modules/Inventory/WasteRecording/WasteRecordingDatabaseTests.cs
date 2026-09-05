using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Recipes.Units;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.WasteRecording.Tests;

public sealed class WasteRecordingTestDb : PgTestDatabase
{
    public WasteRecordingTestDb() : base("alkaros_inv_wst_test_") { }

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

        var migration060 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-waste-records.up.sql");
        var sql060 = await File.ReadAllTextAsync(migration060);
        await RunAsync(DataSource, sql060);
    }
}

public sealed class WasteRecordingDatabaseTests : IClassFixture<WasteRecordingTestDb>
{
    private readonly WasteRecordingTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly PostgresStockBalanceRepository _balanceRepo;
    private readonly PostgresWasteRecordRepository _wasteRepo;
    private readonly StockBalanceProjector _projector;
    private readonly StockMasterService _masterService;
    private readonly WasteRecordingService _wasteService;

    public WasteRecordingDatabaseTests(WasteRecordingTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _movementRepo = new PostgresStockMovementRepository(db.DataSource);
        _balanceRepo = new PostgresStockBalanceRepository(db.DataSource);
        _wasteRepo = new PostgresWasteRecordRepository(db.DataSource);
        _projector = new StockBalanceProjector(_balanceRepo, _movementRepo, _locationRepo);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);
        _wasteService = new WasteRecordingService(
            _wasteRepo, _movementRepo, _itemRepo, _locationRepo, _balanceRepo, _projector, unitConverter);
    }

    [Fact]
    public async Task FullWasteRecordingLifecyclePersistsAndEnforcesImmutability()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Storage", StockLocationType.ColdStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Tomatoes", StockItemType.RawMaterial, "kg");

        var staffId = Guid.NewGuid();

        // 1. Receive initial stock (+20 kg)
        var receiptMovement = new StockMovement(
            id: Guid.NewGuid(),
            stockItemId: item.Id,
            stockLocationId: loc.Id,
            movementType: StockMovementType.PurchaseReceipt,
            direction: MovementDirection.In,
            quantity: 20m,
            unitCode: "kg",
            sourceType: StockMovementSourceType.GoodsReceipt,
            reason: "Initial supplier delivery",
            createdBy: staffId);

        await _movementRepo.AppendAsync(receiptMovement);
        await _projector.ApplyMovementAsync(receiptMovement);

        var balInitial = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balInitial!.OnHandQuantity.Should().Be(20m);

        // 2. Record waste of 4 kg due to spoilage
        var wasteReq = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Spoilage,
            Quantity: 4m,
            UnitCode: "kg",
            Reason: "Rotten tomatoes removed from crate",
            RecordedBy: staffId,
            IdempotencyKey: "waste-" + Guid.NewGuid().ToString("N"));

        var result = await _wasteService.RecordWasteAsync(wasteReq);
        result.IsIdempotentReplay.Should().BeFalse();
        result.Record.NormalizedQuantity.Should().Be(4m);

        // 3. Verify balance decreased to 16 kg
        var balAfter = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        balAfter!.OnHandQuantity.Should().Be(16m);

        // 4. Verify DB read of waste record
        var readRecord = await _wasteRepo.GetByIdAsync(result.Record.Id);
        readRecord.Should().NotBeNull();
        readRecord!.WasteReason.Should().Be("Rotten tomatoes removed from crate");
        readRecord.StockMovementId.Should().Be(result.Movement.Id);

        // 5. Verify immutability trigger blocks updates to waste_records
        var updateSql = @"UPDATE inventory.waste_records SET waste_reason = 'tampered' WHERE id = $1;";
        await using var cmd = _db.DataSource.CreateCommand(updateSql);
        cmd.Parameters.AddWithValue(result.Record.Id);
        var act = async () => await cmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*Waste records are immutable audit records and cannot be updated or deleted*");
    }

    [Fact]
    public async Task IdempotentWasteRecordingInDatabasePreventsDuplicateMovements()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Pantry", StockLocationType.DryStorage);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Milk", StockItemType.RawMaterial, "l");

        var staffId = Guid.NewGuid();

        // Receive 50 liters
        var receipt = new StockMovement(
            id: Guid.NewGuid(),
            stockItemId: item.Id,
            stockLocationId: loc.Id,
            movementType: StockMovementType.PurchaseReceipt,
            direction: MovementDirection.In,
            quantity: 50m,
            unitCode: "l",
            sourceType: StockMovementSourceType.GoodsReceipt,
            createdBy: staffId);
        await _movementRepo.AppendAsync(receipt);
        await _projector.ApplyMovementAsync(receipt);

        var idempKey = "milk-spill-" + Guid.NewGuid().ToString("N");
        var wasteReq = new RecordWasteRequest(
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            WasteSource: WasteSources.Manual,
            Quantity: 5m,
            UnitCode: "l",
            Reason: "Spill on floor",
            RecordedBy: staffId,
            IdempotencyKey: idempKey);

        var res1 = await _wasteService.RecordWasteAsync(wasteReq);
        res1.IsIdempotentReplay.Should().BeFalse();

        var res2 = await _wasteService.RecordWasteAsync(wasteReq);
        res2.IsIdempotentReplay.Should().BeTrue();
        res2.Record.Id.Should().Be(res1.Record.Id);
        res2.Movement.Id.Should().Be(res1.Movement.Id);

        var bal = await _balanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        bal!.OnHandQuantity.Should().Be(45m);
    }
}
