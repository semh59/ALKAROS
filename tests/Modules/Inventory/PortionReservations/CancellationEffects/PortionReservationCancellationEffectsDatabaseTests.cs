using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects.Tests;

public sealed class PortionCancellationEffectsTestDb : PgTestDatabase
{
    public PortionCancellationEffectsTestDb() : base("alkaros_rsv_canc_test_") { }

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

        var migration063 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "063-waste-records.up.sql");
        var sql063 = await File.ReadAllTextAsync(migration063);
        await RunAsync(DataSource, sql063);

        var migration064 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "064-portion-reservations.up.sql");
        var sql064 = await File.ReadAllTextAsync(migration064);
        await RunAsync(DataSource, sql064);

        var migration065 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-reservation-balance-projection.up.sql");
        var sql065 = await File.ReadAllTextAsync(migration065);
        await RunAsync(DataSource, sql065);

        const string kitchenSql = @"
            CREATE SCHEMA IF NOT EXISTS kitchen;
            CREATE TABLE IF NOT EXISTS kitchen.kitchen_ticket_items (
                id UUID PRIMARY KEY,
                ticket_id UUID NOT NULL,
                order_item_id UUID NOT NULL,
                product_id UUID NOT NULL,
                product_name_snapshot VARCHAR(255) NOT NULL,
                quantity NUMERIC(10,3) NOT NULL,
                status VARCHAR(32) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );";
        await RunAsync(DataSource, kitchenSql);
    }
}

public sealed class PortionReservationCancellationEffectsDatabaseTests : IClassFixture<PortionCancellationEffectsTestDb>
{
    private readonly PortionCancellationEffectsTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockMovementRepository _movementRepo;
    private readonly PostgresStockBalanceRepository _stockBalanceRepo;
    private readonly PostgresPortionReservationRepository _reservationRepo;
    private readonly PostgresReservationBalanceRepository _resBalanceRepo;
    private readonly PostgresWasteRecordRepository _wasteRepo;
    private readonly PostgresKitchenItemStateProvider _kitchenProvider;
    private readonly StockMasterService _masterService;
    private readonly PortionReservationLifecycleService _lifecycleService;
    private readonly ReservationBalanceProjector _balanceProjector;
    private readonly StockBalanceProjector _stockProjector;
    private readonly WasteRecordingService _wasteService;
    private readonly PortionCancellationDecisionService _decisionService;

    public PortionReservationCancellationEffectsDatabaseTests(PortionCancellationEffectsTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _movementRepo = new PostgresStockMovementRepository(db.DataSource);
        _stockBalanceRepo = new PostgresStockBalanceRepository(db.DataSource);
        _reservationRepo = new PostgresPortionReservationRepository(db.DataSource);
        _resBalanceRepo = new PostgresReservationBalanceRepository(db.DataSource);
        _wasteRepo = new PostgresWasteRecordRepository(db.DataSource);
        _kitchenProvider = new PostgresKitchenItemStateProvider(db.DataSource);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);

        _lifecycleService = new PortionReservationLifecycleService(_reservationRepo, _itemRepo, _locationRepo);
        _balanceProjector = new ReservationBalanceProjector(_resBalanceRepo);
        _stockProjector = new StockBalanceProjector(_stockBalanceRepo, _movementRepo, _locationRepo);
        _wasteService = new WasteRecordingService(_wasteRepo, _movementRepo, _itemRepo, _locationRepo, _stockBalanceRepo, _stockProjector, unitConverter);

        _decisionService = new PortionCancellationDecisionService(
            _reservationRepo, _lifecycleService, _balanceProjector, _wasteService, _kitchenProvider);
    }

    [Fact]
    public async Task FullPersistencePreKitchenCancellationFlowReleasesAndRestoresAvailable()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen Line", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Cod Fillet", StockItemType.RawMaterial, "portion");

        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 10m);

        var staffId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var rsv = (await _lifecycleService.CreateReservationAsync(new CreateReservationCommand(
            OrderId: orderId, OrderItemId: orderItemId, StockItemId: item.Id, StockLocationId: loc.Id,
            Quantity: 2m, UnitCode: "portion", CreatedBy: staffId))).Reservation;
        await _balanceProjector.ApplyReservationCreatedAsync(rsv);

        var b1 = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        b1!.ReservedQuantity.Should().Be(2m);
        b1.AvailableQuantity.Should().Be(8m);

        await InsertKitchenItemAsync(orderItemId, "Queued");

        var cmd = new ProcessCancellationCommand(rsv.Id, orderItemId, staffId, "Guest cancelled before cooking");
        var res = await _decisionService.ProcessCancellationAsync(cmd);

        res.Action.Should().Be(CancellationAction.Release);
        res.IsIdempotentReplay.Should().BeFalse();

        var updatedRsv = await _reservationRepo.GetByIdAsync(rsv.Id);
        updatedRsv!.Status.Should().Be(PortionReservationStatus.Released);

        var b2 = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        b2!.OnHandQuantity.Should().Be(10m);
        b2.ReservedQuantity.Should().Be(0m);
        b2.AvailableQuantity.Should().Be(10m);

        var retry = await _decisionService.ProcessCancellationAsync(cmd);
        retry.IsIdempotentReplay.Should().BeTrue();
    }

    [Fact]
    public async Task FullPersistencePostPreparationCancellationFlowWastesAndDecrementsOnHand()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen Line 2", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Beef Tenderloin", StockItemType.RawMaterial, "portion");

        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 10m);

        var staffId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var rsv = (await _lifecycleService.CreateReservationAsync(new CreateReservationCommand(
            OrderId: orderId, OrderItemId: orderItemId, StockItemId: item.Id, StockLocationId: loc.Id,
            Quantity: 3m, UnitCode: "portion", CreatedBy: staffId))).Reservation;
        await _balanceProjector.ApplyReservationCreatedAsync(rsv);

        await InsertKitchenItemAsync(orderItemId, "Preparing");

        var cmd = new ProcessCancellationCommand(rsv.Id, orderItemId, staffId, "Guest cancelled while cooking");
        var res = await _decisionService.ProcessCancellationAsync(cmd);

        res.Action.Should().Be(CancellationAction.Waste);
        res.IsIdempotentReplay.Should().BeFalse();
        res.WasteRecord.Should().NotBeNull();

        var updatedRsv = await _reservationRepo.GetByIdAsync(rsv.Id);
        updatedRsv!.Status.Should().Be(PortionReservationStatus.Waste);

        var b = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        b!.OnHandQuantity.Should().Be(7m);
        b.ReservedQuantity.Should().Be(0m);
        b.AvailableQuantity.Should().Be(7m);

        var wastes = await _wasteRepo.GetBySourceAsync(WasteSources.PortionReservation, rsv.Id);
        wastes.Should().HaveCount(1);
        wastes[0].Quantity.Should().Be(3m);

        var retry = await _decisionService.ProcessCancellationAsync(cmd);
        retry.IsIdempotentReplay.Should().BeTrue();
    }

    [Fact]
    public async Task RebuildProjectionMatchesCancellationEffectsExactly()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Kitchen", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Duck Breast", StockItemType.RawMaterial, "portion");

        var staffId = Guid.NewGuid();
        var mvt = StockMovement.Create(
            item.Id, loc.Id, StockMovementType.PurchaseReceipt, 15m, "portion",
            StockMovementSourceType.Manual, direction: MovementDirection.In,
            sourceReferenceId: Guid.NewGuid(), reason: "Initial Stock", createdBy: staffId);
        await _movementRepo.AppendAsync(mvt);
        await _stockProjector.ApplyMovementAsync(mvt);

        var orderItemId = Guid.NewGuid();
        var rsv = (await _lifecycleService.CreateReservationAsync(new CreateReservationCommand(
            OrderId: Guid.NewGuid(), OrderItemId: orderItemId, StockItemId: item.Id, StockLocationId: loc.Id,
            Quantity: 3m, UnitCode: "portion", CreatedBy: staffId))).Reservation;
        await _balanceProjector.ApplyReservationCreatedAsync(rsv);

        await InsertKitchenItemAsync(orderItemId, "Ready");
        await _decisionService.ProcessCancellationAsync(new ProcessCancellationCommand(rsv.Id, orderItemId, staffId));

        var preBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        preBal!.OnHandQuantity.Should().Be(12m);
        preBal.ReservedQuantity.Should().Be(0m);
        preBal.AvailableQuantity.Should().Be(12m);

        await _stockBalanceRepo.ResetAllBalancesAsync();
        await _stockProjector.RebuildAllBalancesAsync();
        await _balanceProjector.RebuildReservationBalancesAsync();

        var postBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        postBal!.OnHandQuantity.Should().Be(12m);
        postBal.ReservedQuantity.Should().Be(0m);
        postBal.AvailableQuantity.Should().Be(12m);
    }

    private async Task InsertKitchenItemAsync(Guid orderItemId, string status)
    {
        const string sql = @"
            INSERT INTO kitchen.kitchen_ticket_items (
                id, ticket_id, order_item_id, product_id, product_name_snapshot, quantity, status, created_at
            ) VALUES (
                $1, $2, $3, $4, 'Test Product', 1, $5, NOW()
            );";
        await using var cmd = _db.DataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(orderItemId);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(status);
        await cmd.ExecuteNonQueryAsync();
    }
}
