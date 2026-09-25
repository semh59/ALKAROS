using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PortionReservations.Lifecycle.Tests;

public sealed class PortionReservationTestDb : PgTestDatabase
{
    public PortionReservationTestDb() : base("alkaros_inv_rsv_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration059 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql");
        var sql059 = await File.ReadAllTextAsync(migration059);
        await RunAsync(DataSource, sql059);

        var migration118 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "118-stock-items-reorder-point.up.sql");
        await RunAsync(DataSource, await File.ReadAllTextAsync(migration118));

        var migration064 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "064-portion-reservations.up.sql");
        var sql064 = await File.ReadAllTextAsync(migration064);
        await RunAsync(DataSource, sql064);
    }
}

public sealed class PortionReservationLifecycleDatabaseTests : IClassFixture<PortionReservationTestDb>
{
    private readonly PortionReservationTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresPortionReservationRepository _reservationRepo;
    private readonly StockMasterService _masterService;
    private readonly PortionReservationLifecycleService _lifecycleService;

    public PortionReservationLifecycleDatabaseTests(PortionReservationTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _reservationRepo = new PostgresPortionReservationRepository(db.DataSource);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);
        _lifecycleService = new PortionReservationLifecycleService(_reservationRepo, _itemRepo, _locationRepo);
    }

    [Fact]
    public async Task FullReservationLifecyclePersistenceFlow()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen 1", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Sea Bass", StockItemType.RawMaterial, "portion");

        var staffId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        // 1. Create reservation
        var createCmd = new CreateReservationCommand(
            OrderId: orderId,
            OrderItemId: orderItemId,
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 2m,
            UnitCode: "portion",
            CreatedBy: staffId,
            IdempotencyKey: "rsv-" + Guid.NewGuid().ToString("N"));

        var createResult = await _lifecycleService.CreateReservationAsync(createCmd);
        createResult.IsIdempotentReplay.Should().BeFalse();
        createResult.Reservation.Status.Should().Be(PortionReservationStatus.Reserved);

        // 2. Read from DB
        var read1 = await _reservationRepo.GetByIdAsync(createResult.Reservation.Id);
        read1.Should().NotBeNull();
        read1!.Status.Should().Be(PortionReservationStatus.Reserved);
        read1.Version.Should().Be(1);

        // 3. Consume reservation
        var consumeCmd = new TransitionReservationCommand(
            read1.Id, PortionReservationStatus.Consumed, staffId, "Cooking completed");
        var consumeResult = await _lifecycleService.ConsumeReservationAsync(consumeCmd);
        consumeResult.IsIdempotentReplay.Should().BeFalse();
        consumeResult.Reservation.Status.Should().Be(PortionReservationStatus.Consumed);
        consumeResult.Reservation.Version.Should().Be(2);

        // 4. Read after consume
        var read2 = await _reservationRepo.GetByIdAsync(createResult.Reservation.Id);
        read2!.Status.Should().Be(PortionReservationStatus.Consumed);
        read2.Version.Should().Be(2);
        read2.TransitionReason.Should().Be("Cooking completed");
    }

    [Fact]
    public async Task ConsumeReleaseRaceAllowsExactlyOneTerminalTransition()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Kitchen 2", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Ribeye", StockItemType.RawMaterial, "portion");

        var staffId = Guid.NewGuid();
        var createCmd = new CreateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 1m,
            UnitCode: "portion",
            CreatedBy: staffId);

        var createResult = await _lifecycleService.CreateReservationAsync(createCmd);
        var rsvId = createResult.Reservation.Id;

        var consumeTask = Task.Run(() => _lifecycleService.ConsumeReservationAsync(
            new TransitionReservationCommand(rsvId, PortionReservationStatus.Consumed, staffId, "Cooked")));
        var releaseTask = Task.Run(() => _lifecycleService.ReleaseReservationAsync(
            new TransitionReservationCommand(rsvId, PortionReservationStatus.Released, staffId, "Cancelled")));

        var results = await Task.WhenAll(
            Task.Run(async () => {
                try { return (Success: true, Result: await consumeTask, Ex: (Exception?)null); }
                catch (Exception ex) { return (Success: false, Result: (ReservationTransitionResult?)null, Ex: (Exception?)ex); }
            }),
            Task.Run(async () => {
                try { return (Success: true, Result: await releaseTask, Ex: (Exception?)null); }
                catch (Exception ex) { return (Success: false, Result: (ReservationTransitionResult?)null, Ex: (Exception?)ex); }
            })
        );

        var successes = results.Count(r => r.Success);
        var failures = results.Count(r => !r.Success);

        // Exactly one transition succeeded!
        successes.Should().Be(1);
        failures.Should().Be(1);

        // The failed one must be PortionReservationConflictException or InvalidPortionReservationTransitionException
        var failedResult = results.First(r => !r.Success);
        failedResult.Ex.Should().Match(ex => ex is PortionReservationConflictException || ex is InvalidPortionReservationTransitionException);

        // Final state in DB is whichever succeeded
        var finalReservation = await _reservationRepo.GetByIdAsync(rsvId);
        finalReservation!.Status.Should().BeOneOf(PortionReservationStatus.Consumed, PortionReservationStatus.Released);
    }
}
