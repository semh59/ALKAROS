using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.ReservationBalanceProjection.Tests;

public sealed class ReservationBalanceTestDb : PgTestDatabase
{
    public ReservationBalanceTestDb() : base("alkaros_rsv_proj_test_") { }

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

        var migration064 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "064-portion-reservations.up.sql");
        var sql064 = await File.ReadAllTextAsync(migration064);
        await RunAsync(DataSource, sql064);

        var migration065 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-reservation-balance-projection.up.sql");
        var sql065 = await File.ReadAllTextAsync(migration065);
        await RunAsync(DataSource, sql065);
    }
}

public sealed class ReservationBalanceProjectionDatabaseTests : IClassFixture<ReservationBalanceTestDb>
{
    private readonly ReservationBalanceTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockBalanceRepository _stockBalanceRepo;
    private readonly PostgresPortionReservationRepository _reservationRepo;
    private readonly PostgresReservationBalanceRepository _projBalanceRepo;
    private readonly StockMasterService _masterService;
    private readonly PortionReservationLifecycleService _lifecycleService;
    private readonly ReservationBalanceProjector _projector;

    public ReservationBalanceProjectionDatabaseTests(ReservationBalanceTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _stockBalanceRepo = new PostgresStockBalanceRepository(db.DataSource);
        _reservationRepo = new PostgresPortionReservationRepository(db.DataSource);
        _projBalanceRepo = new PostgresReservationBalanceRepository(db.DataSource);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);
        _lifecycleService = new PortionReservationLifecycleService(_reservationRepo, _itemRepo, _locationRepo);
        _projector = new ReservationBalanceProjector(_projBalanceRepo);
    }

    [Fact]
    public async Task FullReservationBalancePersistenceFlow()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen Line", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Salmon Portion", StockItemType.RawMaterial, "portion");

        // 1. Initial on-hand = 20
        var bal1 = await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 20m);
        bal1.OnHandQuantity.Should().Be(20m);
        bal1.ReservedQuantity.Should().Be(0m);
        bal1.AvailableQuantity.Should().Be(20m);

        // 2. Create reservation of 5 portions
        var staffId = Guid.NewGuid();
        var createCmd = new CreateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 5m,
            UnitCode: "portion",
            CreatedBy: staffId);

        var createResult = await _lifecycleService.CreateReservationAsync(createCmd);
        var rsv = createResult.Reservation;

        // 3. Apply created reservation to projection
        var applyResult = await _projector.ApplyReservationCreatedAsync(rsv);
        applyResult.IsIdempotentReplay.Should().BeFalse();
        applyResult.Balance.OnHandQuantity.Should().Be(20m);
        applyResult.Balance.ReservedQuantity.Should().Be(5m);
        applyResult.Balance.AvailableQuantity.Should().Be(15m);

        // Verify in DB directly
        var dbBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        dbBal.Should().NotBeNull();
        dbBal!.ReservedQuantity.Should().Be(5m);
        dbBal.AvailableQuantity.Should().Be(15m);

        // Idempotency: replay creation
        var replayCreate = await _projector.ApplyReservationCreatedAsync(rsv);
        replayCreate.IsIdempotentReplay.Should().BeTrue();
        replayCreate.Balance.ReservedQuantity.Should().Be(5m);
        replayCreate.Balance.AvailableQuantity.Should().Be(15m);

        // 4. Consume reservation
        var consumeCmd = new TransitionReservationCommand(rsv.Id, PortionReservationStatus.Consumed, staffId, "Cooked");
        var consumeResult = await _lifecycleService.ConsumeReservationAsync(consumeCmd);

        // Apply terminal transition to projection
        var applyConsume = await _projector.ApplyReservationTransitionAsync(
            consumeResult.Reservation, PortionReservationStatus.Reserved);
        applyConsume.IsIdempotentReplay.Should().BeFalse();
        applyConsume.Balance.ReservedQuantity.Should().Be(0m);
        applyConsume.Balance.AvailableQuantity.Should().Be(20m);

        // Verify in DB
        var dbBal2 = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        dbBal2!.ReservedQuantity.Should().Be(0m);
        dbBal2.AvailableQuantity.Should().Be(20m);

        // Idempotency: replay terminal transition
        var replayConsume = await _projector.ApplyReservationTransitionAsync(
            consumeResult.Reservation, PortionReservationStatus.Reserved);
        replayConsume.IsIdempotentReplay.Should().BeTrue();
        replayConsume.Balance.ReservedQuantity.Should().Be(0m);
        replayConsume.Balance.AvailableQuantity.Should().Be(20m);
    }

    [Fact]
    public async Task ConcurrentTerminalTransitionsAllowExactlyOneTerminalEffect()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Grill Station", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "T-Bone Steak", StockItemType.RawMaterial, "portion");

        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 10m);

        var staffId = Guid.NewGuid();
        var createCmd = new CreateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 4m,
            UnitCode: "portion",
            CreatedBy: staffId);

        var createResult = await _lifecycleService.CreateReservationAsync(createCmd);
        var rsv = createResult.Reservation;
        await _projector.ApplyReservationCreatedAsync(rsv);

        // Verify starting projection: Reserved = 4, Available = 6
        var preBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        preBal!.ReservedQuantity.Should().Be(4m);
        preBal.AvailableQuantity.Should().Be(6m);

        // Concurrently simulate applying terminal events (e.g. race between consume and release)
        var dummyConsumedRsv = new PortionReservation(
            id: rsv.Id,
            orderId: rsv.OrderId,
            orderItemId: rsv.OrderItemId,
            stockItemId: rsv.StockItemId,
            stockLocationId: rsv.StockLocationId,
            quantity: rsv.Quantity,
            unitCode: rsv.UnitCode,
            status: PortionReservationStatus.Consumed,
            version: 2,
            reservedAt: rsv.ReservedAt,
            createdBy: staffId,
            idempotencyKey: rsv.IdempotencyKey,
            transitionedAt: DateTimeOffset.UtcNow,
            transitionReason: "Cooked",
            transitionedBy: staffId);

        var dummyReleasedRsv = new PortionReservation(
            id: rsv.Id,
            orderId: rsv.OrderId,
            orderItemId: rsv.OrderItemId,
            stockItemId: rsv.StockItemId,
            stockLocationId: rsv.StockLocationId,
            quantity: rsv.Quantity,
            unitCode: rsv.UnitCode,
            status: PortionReservationStatus.Released,
            version: 2,
            reservedAt: rsv.ReservedAt,
            createdBy: staffId,
            idempotencyKey: rsv.IdempotencyKey,
            transitionedAt: DateTimeOffset.UtcNow,
            transitionReason: "Cancelled",
            transitionedBy: staffId);

        var task1 = Task.Run(() => _projector.ApplyReservationTransitionAsync(dummyConsumedRsv, PortionReservationStatus.Reserved));
        var task2 = Task.Run(() => _projector.ApplyReservationTransitionAsync(dummyReleasedRsv, PortionReservationStatus.Reserved));

        var results = await Task.WhenAll(task1, task2);

        // Exactly one transition applied, the other was identified as idempotent replay
        var appliedCount = results.Count(r => !r.IsIdempotentReplay);
        var replayCount = results.Count(r => r.IsIdempotentReplay);

        appliedCount.Should().Be(1);
        replayCount.Should().Be(1);

        // Balance in DB is decremented exactly ONCE (never negative reserved or 10+4 available)
        var postBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        postBal!.ReservedQuantity.Should().Be(0m);
        postBal.AvailableQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task RebuildProjectionFromLedgerAndReservationsReconstructsIdenticalBalances()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Prep", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Caesar Salad Portion", StockItemType.RawMaterial, "portion");

        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 25m);

        var staffId = Guid.NewGuid();
        // Reservation 1: 3 portions
        var rsv1 = (await _lifecycleService.CreateReservationAsync(new CreateReservationCommand(
            OrderId: Guid.NewGuid(), OrderItemId: Guid.NewGuid(), StockItemId: item.Id, StockLocationId: loc.Id,
            Quantity: 3m, UnitCode: "portion", CreatedBy: staffId))).Reservation;
        await _projector.ApplyReservationCreatedAsync(rsv1);

        // Reservation 2: 4 portions
        var rsv2 = (await _lifecycleService.CreateReservationAsync(new CreateReservationCommand(
            OrderId: Guid.NewGuid(), OrderItemId: Guid.NewGuid(), StockItemId: item.Id, StockLocationId: loc.Id,
            Quantity: 4m, UnitCode: "portion", CreatedBy: staffId))).Reservation;
        await _projector.ApplyReservationCreatedAsync(rsv2);

        // Initial projection: Reserved = 7, Available = 18
        var bal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        bal!.ReservedQuantity.Should().Be(7m);
        bal.AvailableQuantity.Should().Be(18m);

        // 1. Artificially tamper with balance to simulate crash/projection drift
        await _projBalanceRepo.SetExactReservedBalanceAsync(item.Id, loc.Id, 0m);

        // 2. Drift detection identifies mismatch
        var driftReport = await _projector.DetectDriftAsync();
        driftReport.HasDrift.Should().BeTrue();
        driftReport.Drifts.Should().Contain(d => d.StockItemId == item.Id && d.ReservedDrift == -7m);

        // 3. Execute rebuild
        var rebuildReport = await _projector.RebuildReservationBalancesAsync();
        rebuildReport.TotalBalancesUpdated.Should().BeGreaterThan(0);

        // 4. Verification: Projection restored to exact state
        var restoredBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        restoredBal!.ReservedQuantity.Should().Be(7m);
        restoredBal.AvailableQuantity.Should().Be(18m);

        // 5. Drift report is completely clean
        var cleanDriftReport = await _projector.DetectDriftAsync();
        cleanDriftReport.HasDrift.Should().BeFalse();
    }
}
