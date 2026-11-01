using ALKAROS.IntegrationContracts;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Measurements;
using ALKAROS.Menu.DailyMenuLifecycle;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Menu.CounterProjection.Tests;

/// <summary>
/// Real reservations through the real arbiter, delivered through the real outbox fan-out into the Menu
/// consumer: the counters must match what the reservations say, measured in ordered portions (not the
/// kilograms held) and counted once per order item.
/// </summary>
public sealed class DailyMenuCounterEndToEndTests : IClassFixture<CounterProjectionTestDb>
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static int _nextBusinessDay;

    private readonly CounterProjectionTestDb _db;
    private readonly PostgresDailyMenuCounterProjector _projector;
    private readonly PostgresCrossChannelPortionArbiter _arbiter;
    private readonly PortionReservationLifecycleService _lifecycle;

    public DailyMenuCounterEndToEndTests(CounterProjectionTestDb db)
    {
        _db = db;
        _projector = new PostgresDailyMenuCounterProjector(db.DataSource);
        var items = new PostgresStockItemRepository(db.DataSource);
        var locations = new PostgresStockLocationRepository(db.DataSource);
        var reservations = new PostgresPortionReservationRepository(db.DataSource);
        _lifecycle = new PortionReservationLifecycleService(reservations, items, locations);
        var balances = new PostgresStockBalanceRepository(db.DataSource);
        var waste = new WasteRecordingService(
            new PostgresInventoryTransactionRunner(db.DataSource),
            new PostgresWasteRecordRepository(db.DataSource),
            new PostgresStockMovementRepository(db.DataSource),
            items,
            locations,
            balances,
            new UnitConverter());
        var cancellation = new PortionCancellationDecisionService(
            reservations,
            _lifecycle,
            new ReservationBalanceProjector(new PostgresReservationBalanceRepository(db.DataSource)),
            waste,
            new PostgresKitchenItemStateProvider(db.DataSource));
        _arbiter = new PostgresCrossChannelPortionArbiter(db.DataSource, balances, cancellation);
    }

    private sealed record Seeded(Guid MenuId, Guid MenuItemId, Guid ProductId, IReadOnlyList<Guid> StockItemIds, DateOnly BusinessDate);

    private sealed class FixedBusinessDate(DateOnly date) : IBusinessDateProvider
    {
        public DateOnly GetCurrentBusinessDate(DateTimeOffset? utcNow = null) => date;
    }

    private IIntegrationEventConsumer[] Consumers => [new DailyMenuCounterEventConsumer(_projector)];

    private PostgresDailyMenuItemLocator LocatorFor(DateOnly date) => new PostgresDailyMenuItemLocator(_db.DataSource, new FixedBusinessDate(date));

    /// <summary>A product sold from an Open daily menu, mapped to <paramref name="mappings"/> kilogram stock items, each holding 10 kg.</summary>
    private async Task<Seeded> SeedAsync(int mappings = 1, string menuStatus = "Open", bool itemActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var master = new StockMasterService(
            new PostgresStockLocationRepository(_db.DataSource),
            new PostgresStockItemRepository(_db.DataSource),
            new PostgresProductStockMappingRepository(_db.DataSource),
            new UnitConverter());
        var balances = new PostgresStockBalanceRepository(_db.DataSource);
        var location = await master.CreateLocationAsync("LOC-" + suffix, "Pass", StockLocationType.Kitchen);
        var productId = Guid.NewGuid();
        var stockItemIds = new List<Guid>();
        for (var i = 0; i < mappings; i++)
        {
            var item = await master.CreateStockItemAsync(
                $"SKU-{suffix}-{i}", $"Stock {i}", StockItemType.RawMaterial, "kg", defaultLocationId: location.Id);
            await master.AssignProductToStockItemAsync(productId, item.Id, 0.15m);
            await balances.ApplyOnHandDeltaAsync(item.Id, location.Id, 10m);
            stockItemIds.Add(item.Id);
        }

        var businessDate = new DateOnly(2100, 1, 1).AddDays(Interlocked.Increment(ref _nextBusinessDay));
        var menuId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO catalog.products (product_id, sku, name, base_price, status, created_at, updated_at)
            VALUES (@product, @sku, 'Levrek', 50.00, 'active', NOW(), NOW());
            INSERT INTO menu.daily_menus (daily_menu_id, business_date, status, row_version, created_at, updated_at)
            VALUES (@menu, @date, @status, 1, NOW(), NOW());
            INSERT INTO menu.daily_menu_items (
                daily_menu_item_id, daily_menu_id, product_id, product_name_snapshot, price, planned_portions,
                prepared_portions, available_portions, reserved_portions, consumed_portions, waste_portions,
                out_of_stock, active, created_at, updated_at)
            VALUES (@item, @menu, @product, 'Levrek', 50.00, 20, 0, 0, 0, 0, 0, false, @active, NOW(), NOW());
            """, conn);
        cmd.Parameters.AddWithValue("product", productId);
        cmd.Parameters.AddWithValue("sku", "P-" + suffix);
        cmd.Parameters.AddWithValue("menu", menuId);
        cmd.Parameters.AddWithValue("date", businessDate);
        cmd.Parameters.AddWithValue("status", menuStatus);
        cmd.Parameters.AddWithValue("item", menuItemId);
        cmd.Parameters.AddWithValue("active", itemActive);
        await cmd.ExecuteNonQueryAsync();
        return new Seeded(menuId, menuItemId, productId, stockItemIds, businessDate);
    }

    private async Task<(Guid OrderId, Guid OrderItemId)> ReserveAsync(Seeded seed, decimal portions)
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var menuItems = await LocatorFor(seed.BusinessDate).FindPublishedItemIdsAsync([seed.ProductId]);
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var result = await _arbiter.ReserveAsync(
            new CrossChannelReservationRequest(
                ReservationChannel.Qr,
                orderId.ToString("D"),
                orderId,
                Actor,
                [new CrossChannelReservationLine(
                    orderItemId, seed.ProductId, portions, menuItems.TryGetValue(seed.ProductId, out var id) ? id : null)]),
            conn,
            tx);
        result.IsHeld.Should().BeTrue();
        await tx.CommitAsync();
        return (orderId, orderItemId);
    }

    private async Task<IReadOnlyList<(Guid Id, Guid StockItemId)>> ReservationsOfAsync(Guid orderItemId)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "SELECT id, stock_item_id FROM inventory.portion_reservations WHERE order_item_id = $1 ORDER BY stock_item_id;");
        cmd.Parameters.AddWithValue(orderItemId);
        var rows = new List<(Guid, Guid)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetGuid(0), reader.GetGuid(1)));
        return rows;
    }

    private async Task ConsumeAsync(Guid orderItemId, Guid stockItemId)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var location = await LocationOfAsync(conn, tx, stockItemId);
        await new PostgresReservationAwareConsumptionGuard().ConvertOwnHoldsAndCheckAvailableAsync(
            orderItemId, stockItemId, location, 0.3m, Actor, conn, tx);
        await tx.CommitAsync();
    }

    private static async Task<Guid> LocationOfAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid stockItemId)
    {
        await using var cmd = new NpgsqlCommand("SELECT default_location_id FROM inventory.stock_items WHERE id = $1;", conn, tx);
        cmd.Parameters.AddWithValue(stockItemId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<DailyMenuItemCounters> CountersAsync(Guid menuItemId) =>
        (await _projector.GetCountersAsync(menuItemId))!;

    [Fact]
    public async Task RealReservationsAndTransitionsFillCountersInOrderedPortionsAndMatchTheAuthoritativeSources()
    {
        var seed = await SeedAsync();
        var kept = await ReserveAsync(seed, 2m);
        var consumed = await ReserveAsync(seed, 3m);
        var wasted = await ReserveAsync(seed, 1m);
        var released = await ReserveAsync(seed, 4m);
        var compensated = await ReserveAsync(seed, 5m);

        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(5);
        (await CountersAsync(seed.MenuItemId)).ReservedPortions.Should().Be(15m);

        await ConsumeAsync(consumed.OrderItemId, seed.StockItemIds[0]);
        await _lifecycle.WasteReservationAsync(new TransitionReservationCommand(
            (await ReservationsOfAsync(wasted.OrderItemId)).Single().Id, PortionReservationStatus.Waste, Actor, "dropped"));
        await _lifecycle.ReleaseReservationAsync(new TransitionReservationCommand(
            (await ReservationsOfAsync(released.OrderItemId)).Single().Id, PortionReservationStatus.Released, Actor, "guest left"));
        await _arbiter.CompensateAsync(compensated.OrderId, Actor, "provider refused");
        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(4);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ReservedPortions.Should().Be(2m, "only the first order is still held");
        counters.ConsumedPortions.Should().Be(3m);
        counters.WastePortions.Should().Be(1m);
        counters.PreparedPortions.Should().Be(0m);
        counters.AvailablePortions.Should().Be(0m, "nothing was produced, so the available count stays at zero instead of failing");
        counters.IsOutOfStock.Should().BeTrue();

        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
        await _projector.RebuildDailyMenuCountersAsync(seed.MenuId);
        var rebuilt = await CountersAsync(seed.MenuItemId);
        rebuilt.Should().Be(counters);
        (await ReservationsOfAsync(kept.OrderItemId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ACancellationAfterTheKitchenStartedCountsTheReservationAsWaste()
    {
        var seed = await SeedAsync();
        var order = await ReserveAsync(seed, 2m);
        await using (var cmd = _db.DataSource.CreateCommand(
            "INSERT INTO kitchen.kitchen_ticket_items (id, order_item_id, status) VALUES ($1, $2, 'Preparing');"))
        {
            cmd.Parameters.AddWithValue(Guid.NewGuid());
            cmd.Parameters.AddWithValue(order.OrderItemId);
            await cmd.ExecuteNonQueryAsync();
        }

        await _arbiter.CompensateAsync(order.OrderId, Actor, "guest left after cooking started");
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.WastePortions.Should().Be(2m);
        counters.ReservedPortions.Should().Be(0m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    [Fact]
    public async Task AProductMappedToTwoStockItemsCountsOnePortionPerOrderItem()
    {
        var seed = await SeedAsync(mappings: 2);
        var order = await ReserveAsync(seed, 2m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        (await ReservationsOfAsync(order.OrderItemId)).Should().HaveCount(2);
        (await CountersAsync(seed.MenuItemId)).ReservedPortions.Should().Be(2m);

        foreach (var stockItemId in seed.StockItemIds)
            await ConsumeAsync(order.OrderItemId, stockItemId);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ReservedPortions.Should().Be(0m);
        counters.ConsumedPortions.Should().Be(2m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    [Fact]
    public async Task AReservationRolledBackWithItsOrderLeavesTheCountersUntouched()
    {
        var seed = await SeedAsync();
        var menuItems = await LocatorFor(seed.BusinessDate).FindPublishedItemIdsAsync([seed.ProductId]);
        await using (var conn = await _db.DataSource.OpenConnectionAsync())
        await using (var tx = await conn.BeginTransactionAsync())
        {
            var orderId = Guid.NewGuid();
            await _arbiter.ReserveAsync(
                new CrossChannelReservationRequest(
                    ReservationChannel.Online, "ext-1", orderId, Actor,
                    [new CrossChannelReservationLine(Guid.NewGuid(), seed.ProductId, 2m, menuItems[seed.ProductId])]),
                conn,
                tx);
            await tx.RollbackAsync();
        }

        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(0);
        (await CountersAsync(seed.MenuItemId)).ReservedPortions.Should().Be(0m);
    }

    [Fact]
    public async Task ADeliveryThatArrivesTwiceOrOutOfOrderChangesTheCountersOnce()
    {
        var seed = await SeedAsync();
        var consumer = new DailyMenuCounterEventConsumer(_projector);
        var reservationId = Guid.NewGuid();

        async Task DeliverAsync(string status) => await consumer.HandleAsync(
            DailyMenuCounterIntegrationEventTypes.PortionStatusChanged,
            IntegrationEventSerializer.Serialize(new DailyMenuPortionStatusChanged(
                reservationId, seed.MenuItemId, 3m, status, DateTimeOffset.UtcNow)),
            CancellationToken.None);

        await DeliverAsync("Consumed");
        await DeliverAsync("Reserved");
        await DeliverAsync("Consumed");
        await DeliverAsync("Reserved");

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ReservedPortions.Should().Be(0m);
        counters.ConsumedPortions.Should().Be(3m);
    }

    [Fact]
    public async Task AProductionOutputEventAddsPreparedPortionsOnce()
    {
        var seed = await SeedAsync();
        var consumer = new DailyMenuCounterEventConsumer(_projector);
        var payload = IntegrationEventSerializer.Serialize(new DailyMenuProductionOutputRecorded(
            Guid.NewGuid(), seed.MenuItemId, 12m, DateTimeOffset.UtcNow));

        await consumer.HandleAsync(DailyMenuCounterIntegrationEventTypes.ProductionOutputRecorded, payload, CancellationToken.None);
        await consumer.HandleAsync(DailyMenuCounterIntegrationEventTypes.ProductionOutputRecorded, payload, CancellationToken.None);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.PreparedPortions.Should().Be(12m);
        counters.AvailablePortions.Should().Be(12m);
    }

    [Fact]
    public async Task AReservationOfAProductNotOnTheMenuIsNotCounted()
    {
        var seed = await SeedAsync(menuStatus: "Draft");
        var order = await ReserveAsync(seed, 2m);

        (await ReservationsOfAsync(order.OrderItemId)).Should().HaveCount(1);
        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(0);
        (await CountersAsync(seed.MenuItemId)).ReservedPortions.Should().Be(0m);
    }

    [Theory]
    [InlineData("Open", true, 0, true)]
    [InlineData("Draft", true, 0, false)]
    [InlineData("Closed", true, 0, false)]
    [InlineData("Open", false, 0, false)]
    [InlineData("Open", true, 1, false)]
    public async Task TheLocatorFindsOnlyTheActiveItemOfTodaysOpenMenu(string status, bool active, int dayOffset, bool expected)
    {
        var seed = await SeedAsync(menuStatus: status, itemActive: active);

        var found = await LocatorFor(seed.BusinessDate.AddDays(dayOffset)).FindPublishedItemIdsAsync([seed.ProductId, Guid.NewGuid()]);

        if (expected)
            found.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, Guid>(seed.ProductId, seed.MenuItemId));
        else
            found.Should().BeEmpty();
    }

    private async Task SellDirectlyAsync(Seeded seed, Guid orderItemId, decimal portions, ReservationChannel channel)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var location = await LocationOfAsync(conn, tx, seed.StockItemIds[0]);
        await new PostgresReservationAwareConsumptionGuard().RecordDirectSaleAsync(
            new DirectSale(
                Guid.NewGuid(), orderItemId, channel, "order-ref", seed.StockItemIds[0], location, portions * 0.15m, "kg",
                seed.MenuItemId, portions, Actor),
            conn,
            tx);
        await tx.CommitAsync();
    }

    [Fact]
    public async Task DirectSalesFillConsumedPortionsOncePerOrderItemAndMatchRebuildAndDrift()
    {
        var seed = await SeedAsync(mappings: 2);
        var waiter = Guid.NewGuid();
        var nfc = Guid.NewGuid();
        await SellDirectlyAsync(seed, waiter, 3m, ReservationChannel.Waiter);
        await SellDirectlyAsync(seed, waiter, 3m, ReservationChannel.Waiter);
        await SellDirectlyAsync(seed, nfc, 2m, ReservationChannel.Nfc);
        await SellDirectlyAsync(seed, Guid.NewGuid(), 1m, ReservationChannel.Cashier);

        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(3, "the repeated sale writes nothing");
        var counters = await CountersAsync(seed.MenuItemId);
        counters.ConsumedPortions.Should().Be(6m);
        counters.ReservedPortions.Should().Be(0m);
        (await ReservationsOfAsync(waiter)).Should().ContainSingle();

        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
        await _projector.RebuildDailyMenuCountersAsync(seed.MenuId);
        (await CountersAsync(seed.MenuItemId)).Should().Be(counters);
    }

    [Fact]
    public async Task AnItemAlreadyHeldByAnotherChannelIsNotCountedAgainByTheDirectSale()
    {
        var seed = await SeedAsync();
        var order = await ReserveAsync(seed, 2m);
        await ConsumeAsync(order.OrderItemId, seed.StockItemIds[0]);

        await SellDirectlyAsync(seed, order.OrderItemId, 2m, ReservationChannel.Waiter);

        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(2, "the hold's own reserve and consume events");
        (await ReservationsOfAsync(order.OrderItemId)).Should().ContainSingle();
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    private StockMovementReversalService ReversalService()
    {
        var movements = new PostgresStockMovementRepository(_db.DataSource);
        var balances = new PostgresStockBalanceRepository(_db.DataSource);
        var locations = new PostgresStockLocationRepository(_db.DataSource);
        return new StockMovementReversalService(
            movements, new PostgresStockItemRepository(_db.DataSource), locations,
            new StockBalanceProjector(balances, movements, locations), balances);
    }

    private async Task RecordConsumptionAsync(Seeded seed, Guid orderItemId, decimal stockQuantity)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var location = await LocationOfAsync(conn, tx, seed.StockItemIds[0]);
        await new PostgresStockMovementRepository(_db.DataSource).AppendAsync(
            new StockMovement(
                Guid.NewGuid(), seed.StockItemIds[0], location, StockMovementType.Consumption, MovementDirection.Out,
                stockQuantity, "kg", StockMovementSourceType.Order, orderItemId, "test consumption", Actor),
            conn,
            tx);
        await tx.CommitAsync();
    }

    private async Task ReturnShareAsync(Guid orderItemId, decimal returned, decimal previously, decimal total)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await ReversalService().ReturnConsumedShareAsync(
            new StockShareReturnRequest(orderItemId, returned, previously, total, "refund", Actor), conn, tx);
        await tx.CommitAsync();
    }

    [Fact]
    public async Task ARefundThatPutsStockBackGivesTheSameShareOfPortionsBackOnceAndMatchesRebuildAndDrift()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await SellDirectlyAsync(seed, orderItemId, 3m, ReservationChannel.Waiter);
        await RecordConsumptionAsync(seed, orderItemId, 0.45m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(3m);

        await ReturnShareAsync(orderItemId, returned: 1m, previously: 0m, total: 3m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);

        await ReturnShareAsync(orderItemId, returned: 1m, previously: 0m, total: 3m);
        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(0, "the same cumulative share changes nothing");
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);

        await ReturnShareAsync(orderItemId, returned: 2m, previously: 1m, total: 3m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        var counters = await CountersAsync(seed.MenuItemId);
        counters.ConsumedPortions.Should().Be(0m);
        counters.WastePortions.Should().Be(0m);

        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
        await _projector.RebuildDailyMenuCountersAsync(seed.MenuId);
        (await CountersAsync(seed.MenuItemId)).Should().Be(counters);
    }

    private async Task AdjustAsync(Func<PostgresReservationAwareConsumptionGuard, NpgsqlConnection, NpgsqlTransaction, Task> adjust)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await adjust(new PostgresReservationAwareConsumptionGuard(), conn, tx);
        await tx.CommitAsync();
    }

    private Task ReturnPortionsAsync(Guid orderItemId, decimal returned, decimal total) =>
        AdjustAsync((guard, conn, tx) => guard.ReturnPortionsAsync(orderItemId, returned, total, conn, tx));

    private Task WastePortionsAsync(Guid orderItemId) =>
        AdjustAsync((guard, conn, tx) => guard.WastePortionsAsync(orderItemId, conn, tx));

    private async Task ProduceAsync(Seeded seed, decimal quantity) =>
        await new DailyMenuCounterEventConsumer(_projector).HandleAsync(
            DailyMenuCounterIntegrationEventTypes.ProductionOutputRecorded,
            IntegrationEventSerializer.Serialize(new DailyMenuProductionOutputRecorded(
                Guid.NewGuid(), seed.MenuItemId, quantity, DateTimeOffset.UtcNow)),
            CancellationToken.None);

    [Fact]
    public async Task ACancelledItemTheKitchenHadStartedMovesItsPortionsFromConsumedToWasteOnceAndKeepsAvailableAsItWas()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await ProduceAsync(seed, 10m);
        await SellDirectlyAsync(seed, orderItemId, 3m, ReservationChannel.Waiter);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        var sold = await CountersAsync(seed.MenuItemId);
        sold.ConsumedPortions.Should().Be(3m);
        sold.AvailablePortions.Should().Be(7m);

        await WastePortionsAsync(orderItemId);
        await WastePortionsAsync(orderItemId);
        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(1, "the second cancellation announces nothing");

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ConsumedPortions.Should().Be(0m);
        counters.WastePortions.Should().Be(3m);
        counters.AvailablePortions.Should().Be(7m, "a wasted portion is used up just like a sold one");
    }

    [Fact]
    public async Task AWasteAfterAPartialReturnMatchesRebuildAndDrift()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await SellDirectlyAsync(seed, orderItemId, 4m, ReservationChannel.Cashier);
        await ReturnPortionsAsync(orderItemId, 1m, 4m);
        await WastePortionsAsync(orderItemId);
        await ReturnPortionsAsync(orderItemId, 4m, 4m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ConsumedPortions.Should().Be(0m);
        counters.WastePortions.Should().Be(3m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
        await _projector.RebuildDailyMenuCountersAsync(seed.MenuId);
        (await CountersAsync(seed.MenuItemId)).Should().Be(counters);
    }

    [Fact]
    public async Task AGivenBackItemMakesItsPortionsAvailableAgainAndAWasteAfterAPartialReturnOnlyWastesTheRest()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await ProduceAsync(seed, 10m);
        await SellDirectlyAsync(seed, orderItemId, 4m, ReservationChannel.Cashier);
        await ReturnPortionsAsync(orderItemId, 1m, 4m);
        await WastePortionsAsync(orderItemId);
        await ReturnPortionsAsync(orderItemId, 4m, 4m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ConsumedPortions.Should().Be(0m);
        counters.WastePortions.Should().Be(3m, "only the three portions not given back were wasted; a later return cannot take them back");
        counters.AvailablePortions.Should().Be(7m);
    }

    [Fact]
    public async Task AQrItemCancelledAfterAcceptanceGivesBackOnlyTheKeyedReservationOfItsTwoStockMappings()
    {
        var seed = await SeedAsync(mappings: 2);
        var order = await ReserveAsync(seed, 2m);
        foreach (var stockItemId in seed.StockItemIds)
            await ConsumeAsync(order.OrderItemId, stockItemId);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);

        await ReturnPortionsAsync(order.OrderItemId, 2m, 2m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(0m);
        await using var cmd = _db.DataSource.CreateCommand(
            "SELECT count(*) FROM inventory.portion_reservations WHERE order_item_id = $1 AND metadata->>'portions_returned' IS NOT NULL;");
        cmd.Parameters.AddWithValue(order.OrderItemId);
        ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(1);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    [Fact]
    public async Task AnAdjustmentThatOvertakesItsConsumptionOrArrivesTwiceOrLateChangesTheCountersOnce()
    {
        var seed = await SeedAsync();
        var consumer = new DailyMenuCounterEventConsumer(_projector);
        var reservationId = Guid.NewGuid();

        async Task AdjustedAsync(decimal returned, decimal wasted) => await consumer.HandleAsync(
            DailyMenuCounterIntegrationEventTypes.PortionAdjusted,
            IntegrationEventSerializer.Serialize(new DailyMenuPortionAdjusted(
                reservationId, seed.MenuItemId, 3m, returned, wasted, DateTimeOffset.UtcNow)),
            CancellationToken.None);

        await AdjustedAsync(1m, 0m);
        await consumer.HandleAsync(
            DailyMenuCounterIntegrationEventTypes.PortionStatusChanged,
            IntegrationEventSerializer.Serialize(new DailyMenuPortionStatusChanged(
                reservationId, seed.MenuItemId, 3m, "Consumed", DateTimeOffset.UtcNow)),
            CancellationToken.None);
        await AdjustedAsync(1m, 0m);
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);

        await AdjustedAsync(2m, 1m);
        await AdjustedAsync(1m, 0m);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.ConsumedPortions.Should().Be(0m);
        counters.WastePortions.Should().Be(1m);
        counters.ReservedPortions.Should().Be(0m);
    }

    [Fact]
    public async Task ARebuildBeforeTheAdjustmentIsDeliveredDoesNotLetTheDeliveryTakeThePortionsBackTwice()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await SellDirectlyAsync(seed, orderItemId, 3m, ReservationChannel.Waiter);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        await ReturnPortionsAsync(orderItemId, 1m, 3m);

        await _projector.RebuildDailyMenuCountersAsync(seed.MenuId);
        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(2m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    [Fact]
    public async Task ARebuildBeforeAWasteIsDeliveredDoesNotLetTheDeliveryWasteThePortionsTwice()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await SellDirectlyAsync(seed, orderItemId, 3m, ReservationChannel.Waiter);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        await WastePortionsAsync(orderItemId);

        await _projector.RebuildDailyMenuCountersAsync(seed.MenuId);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        var counters = await CountersAsync(seed.MenuItemId);
        counters.WastePortions.Should().Be(3m);
        counters.ConsumedPortions.Should().Be(0m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    [Fact]
    public async Task AFractionalShareRoundsToThreeDecimalsAndASmallerLaterShareTakesNothingBack()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await SellDirectlyAsync(seed, orderItemId, 4m, ReservationChannel.Waiter);
        await ReturnPortionsAsync(orderItemId, 2m, 3m);
        await ReturnPortionsAsync(orderItemId, 1m, 3m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(1.333m);
        (await _projector.DetectDriftAsync(seed.MenuId)).HasDrift.Should().BeFalse();
    }

    [Fact]
    public async Task AnAdjustmentLargerThanWhatIsCountedNeverTakesTheCounterBelowZero()
    {
        var seed = await SeedAsync();
        var orderItemId = Guid.NewGuid();
        await SellDirectlyAsync(seed, orderItemId, 3m, ReservationChannel.Waiter);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);
        await using (var cmd = _db.DataSource.CreateCommand(
            "UPDATE menu.daily_menu_items SET consumed_portions = 1 WHERE daily_menu_item_id = $1;"))
        {
            cmd.Parameters.AddWithValue(seed.MenuItemId);
            await cmd.ExecuteNonQueryAsync();
        }

        await ReturnPortionsAsync(orderItemId, 3m, 3m);
        await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers);

        (await CountersAsync(seed.MenuItemId)).ConsumedPortions.Should().Be(0m);
    }

    [Fact]
    public async Task ANegativeAdjustmentIsRefused()
    {
        var seed = await SeedAsync();

        var act = () => _projector.ApplyReservationAdjustmentAsync(
            new ReservationAdjustmentCounterEvent(seed.MenuItemId, Guid.NewGuid(), -1m, 0m));

        await act.Should().ThrowAsync<InvalidCounterDeltaException>();
    }

    [Fact]
    public async Task AnItemThatWasNeverCountedIsNotAdjusted()
    {
        var seed = await SeedAsync(menuStatus: "Draft");
        var order = await ReserveAsync(seed, 2m);
        await ConsumeAsync(order.OrderItemId, seed.StockItemIds[0]);

        await ReturnPortionsAsync(order.OrderItemId, 2m, 2m);
        await WastePortionsAsync(order.OrderItemId);

        (await OutboxTestDrain.DrainAsync(_db.DataSource, Consumers)).Should().Be(0);
        (await CountersAsync(seed.MenuItemId)).WastePortions.Should().Be(0m);
    }
}
