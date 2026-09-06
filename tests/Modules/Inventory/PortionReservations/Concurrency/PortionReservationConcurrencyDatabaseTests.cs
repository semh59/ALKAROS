using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Recipes.Units;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PortionReservations.Concurrency.Tests;

public sealed class PortionReservationConcurrencyTestDb : PgTestDatabase
{
    public PortionReservationConcurrencyTestDb() : base("alkaros_rsv_conc_test_") { }

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

        var migration064 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "064-portion-reservations.up.sql");
        var sql064 = await File.ReadAllTextAsync(migration064);
        await RunAsync(DataSource, sql064);

        var migration065 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-reservation-balance-projection.up.sql");
        var sql065 = await File.ReadAllTextAsync(migration065);
        await RunAsync(DataSource, sql065);
    }
}

public sealed class PortionReservationConcurrencyDatabaseTests : IClassFixture<PortionReservationConcurrencyTestDb>
{
    private readonly PortionReservationConcurrencyTestDb _db;
    private readonly PostgresStockLocationRepository _locationRepo;
    private readonly PostgresStockItemRepository _itemRepo;
    private readonly PostgresStockBalanceRepository _stockBalanceRepo;
    private readonly StockMasterService _masterService;
    private readonly PostgresPortionReservationArbitratorRepository _arbitratorRepo;
    private readonly PortionReservationArbitrator _arbitrator;

    public PortionReservationConcurrencyDatabaseTests(PortionReservationConcurrencyTestDb db)
    {
        _db = db;
        _locationRepo = new PostgresStockLocationRepository(db.DataSource);
        _itemRepo = new PostgresStockItemRepository(db.DataSource);
        _stockBalanceRepo = new PostgresStockBalanceRepository(db.DataSource);

        var unitConverter = new UnitConverter();
        var mappingRepo = new PostgresProductStockMappingRepository(db.DataSource);
        _masterService = new StockMasterService(_locationRepo, _itemRepo, mappingRepo, unitConverter);

        _arbitratorRepo = new PostgresPortionReservationArbitratorRepository(db.DataSource);
        _arbitrator = new PortionReservationArbitrator(_arbitratorRepo);
    }

    [Fact]
    public async Task FullPersistenceArbitrationSuccessAndOutOfStock()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Cold Counter", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Lobster Tail", StockItemType.RawMaterial, "portion");

        // Seed on-hand = 3 portions
        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 3m);

        var staffId = Guid.NewGuid();

        // 1. Request 2 portions -> Success (1 remaining)
        var cmd1 = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 2m,
            UnitCode: "portion",
            ActorId: staffId);

        var res1 = await _arbitrator.ArbitrateReservationAsync(cmd1);
        res1.Status.Should().Be(ArbitrationStatus.Reserved);
        res1.RemainingAvailableQuantity.Should().Be(1m);

        // 2. Request 2 portions -> OutOfStock (only 1 available)
        var cmd2 = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 2m,
            UnitCode: "portion",
            ActorId: staffId);

        var res2 = await _arbitrator.ArbitrateReservationAsync(cmd2);
        res2.Status.Should().Be(ArbitrationStatus.OutOfStock);
        res2.RemainingAvailableQuantity.Should().Be(1m);

        // 3. Request 1 portion -> Success (0 remaining)
        var cmd3 = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 1m,
            UnitCode: "portion",
            ActorId: staffId);

        var res3 = await _arbitrator.ArbitrateReservationAsync(cmd3);
        res3.Status.Should().Be(ArbitrationStatus.Reserved);
        res3.RemainingAvailableQuantity.Should().Be(0m);

        // DB state check
        var dbBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        dbBal!.ReservedQuantity.Should().Be(3m);
        dbBal.AvailableQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task ParallelArbitrationForLastPortionUnderPostgreSql()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Sushi Bar", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Bluefin Tuna Toro", StockItemType.RawMaterial, "portion");

        // Exactly 1 portion available
        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 1m);

        var staffId = Guid.NewGuid();

        var cmdA = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 1m,
            UnitCode: "portion",
            ActorId: staffId,
            IdempotencyKey: "race-client-a-" + Guid.NewGuid().ToString("N"));

        var cmdB = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 1m,
            UnitCode: "portion",
            ActorId: staffId,
            IdempotencyKey: "race-client-b-" + Guid.NewGuid().ToString("N"));

        var taskA = Task.Run(() => _arbitrator.ArbitrateReservationAsync(cmdA));
        var taskB = Task.Run(() => _arbitrator.ArbitrateReservationAsync(cmdB));

        var results = await Task.WhenAll(taskA, taskB);

        var reservedCount = results.Count(r => r.Status == ArbitrationStatus.Reserved);
        var outOfStockCount = results.Count(r => r.Status == ArbitrationStatus.OutOfStock);

        reservedCount.Should().Be(1);
        outOfStockCount.Should().Be(1);

        // Balance in DB must never be negative
        var finalBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        finalBal!.OnHandQuantity.Should().Be(1m);
        finalBal.ReservedQuantity.Should().Be(1m);
        finalBal.AvailableQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task IdempotencyReplayUnderPostgreSql()
    {
        var locCode = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var loc = await _masterService.CreateLocationAsync(locCode, "Bakery", StockLocationType.Kitchen);

        var itemCode = "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var item = await _masterService.CreateStockItemAsync(itemCode, "Sourdough Bread", StockItemType.RawMaterial, "portion");

        await _stockBalanceRepo.ApplyOnHandDeltaAsync(item.Id, loc.Id, 5m);

        var staffId = Guid.NewGuid();
        var idempKey = "idemp-" + Guid.NewGuid().ToString("N");

        var cmd = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 2m,
            UnitCode: "portion",
            ActorId: staffId,
            IdempotencyKey: idempKey);

        var res1 = await _arbitrator.ArbitrateReservationAsync(cmd);
        var res2 = await _arbitrator.ArbitrateReservationAsync(cmd);

        res1.Status.Should().Be(ArbitrationStatus.Reserved);
        res2.Status.Should().Be(ArbitrationStatus.IdempotentReplay);
        res2.Reservation!.Id.Should().Be(res1.Reservation!.Id);

        // Available quantity decremented only once
        var finalBal = await _stockBalanceRepo.GetByItemAndLocationAsync(item.Id, loc.Id);
        finalBal!.ReservedQuantity.Should().Be(2m);
        finalBal.AvailableQuantity.Should().Be(3m);
    }
}
