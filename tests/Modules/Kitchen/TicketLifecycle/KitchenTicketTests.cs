namespace ALKAROS.Kitchen.TicketLifecycle.Tests;

using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class KitchenTestDatabase : PgTestDatabase
{
    public KitchenTestDatabase()
        : base("alkaros_kit001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class KitchenTicketUnitTests
{
    [Fact]
    public void ItemTransitionsFollowCanonicalContracts()
    {
        var item = new KitchenTicketItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Steak",
            1);

        item.Status.Should().Be(KitchenTicketItemState.Queued);

        // Queued -> Preparing
        var preparing = item.TransitionTo(KitchenTicketItemState.Preparing);
        preparing.Status.Should().Be(KitchenTicketItemState.Preparing);

        // Preparing -> Ready
        var ready = preparing.TransitionTo(KitchenTicketItemState.Ready);
        ready.Status.Should().Be(KitchenTicketItemState.Ready);
        ready.ReadyAt.Should().NotBeNull();

        // Ready -> Served
        var served = ready.TransitionTo(KitchenTicketItemState.Served);
        served.Status.Should().Be(KitchenTicketItemState.Served);
        served.ServedAt.Should().NotBeNull();

        // Terminal state cannot transition
        var act = () => served.TransitionTo(KitchenTicketItemState.Preparing);
        act.Should().Throw<InvalidKitchenTransitionException>();
    }

    [Fact]
    public void ItemCancellationIsAllowedFromNonTerminalStates()
    {
        var item = new KitchenTicketItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Soup",
            1);

        var cancelledFromQueued = item.TransitionTo(KitchenTicketItemState.Cancelled, "Customer changed mind");
        cancelledFromQueued.Status.Should().Be(KitchenTicketItemState.Cancelled);
        cancelledFromQueued.CancellationReason.Should().Be("Customer changed mind");

        var preparing = item.TransitionTo(KitchenTicketItemState.Preparing);
        var cancelledFromPreparing = preparing.TransitionTo(KitchenTicketItemState.Cancelled, "Kitchen spilled");
        cancelledFromPreparing.Status.Should().Be(KitchenTicketItemState.Cancelled);
    }

    [Fact]
    public void ParentReadyRequiresAllNonCancelledItemsToBeReadyOrServed()
    {
        var ticketId = Guid.NewGuid();
        var item1 = new KitchenTicketItem(Guid.NewGuid(), ticketId, Guid.NewGuid(), Guid.NewGuid(), "Burger", 1);
        var item2 = new KitchenTicketItem(Guid.NewGuid(), ticketId, Guid.NewGuid(), Guid.NewGuid(), "Fries", 1);

        var ticket = new KitchenTicket(
            ticketId,
            Guid.NewGuid(),
            "KT-100",
            "Grill",
            [item1, item2],
            status: KitchenTicketState.Accepted);

        // Step 1: Item 1 starts preparing -> Ticket auto-promotes to Preparing
        var t1 = ticket.UpdateItemStatus(item1.Id, KitchenTicketItemState.Preparing);
        t1.Status.Should().Be(KitchenTicketState.Preparing);

        // Step 2: Item 1 finishes -> becomes Ready. Item 2 is still Queued.
        var t2 = t1.UpdateItemStatus(item1.Id, KitchenTicketItemState.Ready);
        t2.Items.First(i => i.Id == item1.Id).Status.Should().Be(KitchenTicketItemState.Ready);
        t2.Items.First(i => i.Id == item2.Id).Status.Should().Be(KitchenTicketItemState.Queued);

        // Acceptance invariant: Mixed Preparing/Ready/Queued is valid, but parent Ready must be rejected!
        t2.CanTransitionTo(KitchenTicketState.Ready).Should().BeFalse();
        var act = () => t2.TransitionTo(KitchenTicketState.Ready);
        act.Should().Throw<InvalidKitchenTransitionException>();

        // Step 3: Item 2 is cancelled (e.g. out of stock)
        var t3 = t2.UpdateItemStatus(item2.Id, KitchenTicketItemState.Cancelled, "Out of potatoes");

        // Now every non-cancelled item (item1) is Ready -> parent Ready is valid!
        t3.CanTransitionTo(KitchenTicketState.Ready).Should().BeTrue();
        var tReady = t3.TransitionTo(KitchenTicketState.Ready);
        tReady.Status.Should().Be(KitchenTicketState.Ready);
        tReady.ReadyAt.Should().NotBeNull();
    }

    [Fact]
    public void ParentCancelledCascadesToActiveItems()
    {
        var ticketId = Guid.NewGuid();
        var item1 = new KitchenTicketItem(Guid.NewGuid(), ticketId, Guid.NewGuid(), Guid.NewGuid(), "Pizza", 1);
        var item2 = new KitchenTicketItem(Guid.NewGuid(), ticketId, Guid.NewGuid(), Guid.NewGuid(), "Salad", 1);

        var ticket = new KitchenTicket(
            ticketId,
            Guid.NewGuid(),
            "KT-200",
            "Oven",
            [item1, item2],
            status: KitchenTicketState.Preparing);

        var cancelledTicket = ticket.TransitionTo(KitchenTicketState.Cancelled, "Entire order cancelled by waiter");
        cancelledTicket.Status.Should().Be(KitchenTicketState.Cancelled);
        cancelledTicket.CancelledAt.Should().NotBeNull();
        cancelledTicket.CancellationReason.Should().Be("Entire order cancelled by waiter");

        cancelledTicket.Items.Should().AllSatisfy(item =>
        {
            item.Status.Should().Be(KitchenTicketItemState.Cancelled);
            item.CancellationReason.Should().Be("Entire order cancelled by waiter");
        });
    }

    [Fact]
    public void WhenAllItemsAreCancelledParentTicketAutoCancels()
    {
        var ticketId = Guid.NewGuid();
        var item1 = new KitchenTicketItem(Guid.NewGuid(), ticketId, Guid.NewGuid(), Guid.NewGuid(), "Drink", 1);

        var ticket = new KitchenTicket(
            ticketId,
            Guid.NewGuid(),
            "KT-300",
            "Bar",
            [item1],
            status: KitchenTicketState.Accepted);

        var updated = ticket.UpdateItemStatus(item1.Id, KitchenTicketItemState.Cancelled, "Customer voided drink");
        updated.Status.Should().Be(KitchenTicketState.Cancelled);
    }

    [Fact]
    public void CreateFromOrderAccuratelyConstructsKitchenTicket()
    {
        var orderId = Guid.NewGuid();
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        var orderItem1 = new OrderItem(
            Guid.NewGuid(), orderId, productId1, "Ribeye Steak", 1, 350m, 10m, "STEAK-01",
            notes: "Medium rare");
        var orderItem2 = new OrderItem(
            Guid.NewGuid(), orderId, productId2, "Mashed Potatoes", 1, 80m, 10m, "SIDE-01");

        var order = new Order(
            orderId,
            OrderSource.Waiter,
            "ORD-99901",
            [orderItem1, orderItem2]);

        var ticket = KitchenTicket.CreateFromOrder(order, "GrillStation");

        ticket.OrderId.Should().Be(orderId);
        ticket.StationId.Should().Be("GrillStation");
        ticket.Status.Should().Be(KitchenTicketState.Queued);
        ticket.Items.Should().HaveCount(2);

        var steakItem = ticket.Items.First(i => i.ProductId == productId1);
        steakItem.ProductNameSnapshot.Should().Be("Ribeye Steak");
        steakItem.Quantity.Should().Be(1);
        steakItem.Notes.Should().Be("Medium rare");
        steakItem.Status.Should().Be(KitchenTicketItemState.Queued);
    }
}

public sealed class PostgresKitchenTicketIntegrationTests : IClassFixture<KitchenTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresKitchenTicketRepository _ticketRepo;
    private readonly PostgresOrderRepository _orderRepo;

    public PostgresKitchenTicketIntegrationTests(KitchenTestDatabase database)
    {
        _dataSource = database.DataSource;
        _ticketRepo = new PostgresKitchenTicketRepository(database.DataSource);
        _orderRepo = new PostgresOrderRepository(database.DataSource);
    }

    private async Task<Order> CreateAndPersistSampleOrderAsync()
    {
        var productId = Guid.NewGuid();
        await using (var cmd = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """))
        {
            cmd.Parameters.AddWithValue("product_id", productId);
            cmd.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("name", "Kebap");
            cmd.Parameters.AddWithValue("product_type", 1);
            cmd.Parameters.AddWithValue("stock_mode", 1);
            cmd.Parameters.AddWithValue("current_price", 200m);
            await cmd.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            Guid.NewGuid(),
            orderId,
            productId,
            "Kebap",
            2,
            200m,
            10m,
            skuSnapshot: "KEBAP-01");

        var order = new Order(
            orderId,
            OrderSource.Waiter,
            "ORD-" + Guid.NewGuid().ToString("N")[..8],
            [item]);

        await _orderRepo.AddAsync(order);
        return order;
    }

    [Fact]
    public async Task TicketLifecycleRoundTripsThroughPostgres()
    {
        var order = await CreateAndPersistSampleOrderAsync();
        var ticket = KitchenTicket.CreateFromOrder(order, "MainKitchen");

        // 1. Add ticket
        await _ticketRepo.AddAsync(ticket);

        var loaded = await _ticketRepo.GetByIdAsync(ticket.Id);
        loaded.Should().NotBeNull();
        loaded!.TicketNumber.Should().Be(ticket.TicketNumber);
        loaded.StationId.Should().Be("MainKitchen");
        loaded.Status.Should().Be(KitchenTicketState.Queued);
        loaded.RowVersion.Should().Be(1);
        loaded.Items.Should().HaveCount(1);

        // 2. Accept and start preparing
        var accepted = loaded.TransitionTo(KitchenTicketState.Accepted);
        var preparing = accepted.TransitionTo(KitchenTicketState.Preparing);
        var updatedItem = preparing.UpdateItemStatus(preparing.Items[0].Id, KitchenTicketItemState.Preparing);

        var newVer = await _ticketRepo.SaveAsync(updatedItem, loaded.RowVersion);
        newVer.Should().Be(2);

        // 3. Mark item ready and complete ticket
        var reloaded = await _ticketRepo.GetByIdAsync(ticket.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Status.Should().Be(KitchenTicketState.Preparing);

        var itemReady = reloaded.UpdateItemStatus(reloaded.Items[0].Id, KitchenTicketItemState.Ready);
        var ticketReady = itemReady.TransitionTo(KitchenTicketState.Ready);

        var finalVer = await _ticketRepo.SaveAsync(ticketReady, reloaded.RowVersion);
        finalVer.Should().Be(3);

        var completed = await _ticketRepo.GetByIdAsync(ticket.Id);
        completed!.Status.Should().Be(KitchenTicketState.Ready);
        completed.ReadyAt.Should().NotBeNull();
        completed.Items[0].Status.Should().Be(KitchenTicketItemState.Ready);
    }

    [Fact]
    public async Task SaveAsyncEnforcesOptimisticConcurrency()
    {
        var order = await CreateAndPersistSampleOrderAsync();
        var ticket = KitchenTicket.CreateFromOrder(order, "Bar");

        await _ticketRepo.AddAsync(ticket);

        var accepted = ticket.TransitionTo(KitchenTicketState.Accepted);
        await _ticketRepo.SaveAsync(accepted, expectedRowVersion: 1);

        // Concurrent update with stale version 1 must throw
        var conflicting = ticket.TransitionTo(KitchenTicketState.Cancelled, "Cancelled concurrently");
        var act = () => _ticketRepo.SaveAsync(conflicting, expectedRowVersion: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*concurrent modification*");
    }

    [Fact]
    public async Task GetActiveByStationFiltersNonTerminalTickets()
    {
        var order = await CreateAndPersistSampleOrderAsync();
        var ticket1 = KitchenTicket.CreateFromOrder(order, "PastryStation", ticketNumber: "KT-P1");
        var ticket2 = KitchenTicket.CreateFromOrder(order, "PastryStation", ticketNumber: "KT-P2");

        await _ticketRepo.AddAsync(ticket1);
        await _ticketRepo.AddAsync(ticket2);

        // Cancel ticket2
        var cancelled2 = ticket2.TransitionTo(KitchenTicketState.Cancelled);
        await _ticketRepo.SaveAsync(cancelled2, expectedRowVersion: 1);

        var activeTickets = await _ticketRepo.GetActiveByStationAsync("PastryStation");
        activeTickets.Should().ContainSingle(t => t.Id == ticket1.Id);
    }
}

public sealed class PostgresKitchenDownSqlTests : IClassFixture<KitchenTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresKitchenDownSqlTests(KitchenTestDatabase database)
    {
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task DownSqlDropsKitchenSchemaAndTables()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "014-kitchen-tickets.down.sql"));

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = downSql;
        await cmd.ExecuteNonQueryAsync();

        // Verify table no longer exists
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText =
            """
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'kitchen' AND table_name = 'kitchen_tickets'
            );
            """;
        var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
        exists.Should().BeFalse();
    }
}
