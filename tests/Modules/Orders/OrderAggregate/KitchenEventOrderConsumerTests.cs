using ALKAROS.IntegrationContracts;
using ALKAROS.Orders.Integration;
using ALKAROS.Orders.OrderAggregate.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace ALKAROS.Orders.OrderAggregate.Tests;

/// <summary>
/// V1-KIT-005: Order's reaction to a Kitchen ticket item state change, over
/// the real repository and database — same shape as the (untested, but
/// structurally identical) TableEventOrderConsumer.
/// </summary>
public sealed class KitchenEventOrderConsumerTests : IClassFixture<OrdersTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresOrderRepository _orders;
    private readonly KitchenEventOrderConsumer _consumer;
    private readonly CapturingLogger _logger = new();

    public KitchenEventOrderConsumerTests(OrdersTestDatabase database)
    {
        _dataSource = database.DataSource;
        _orders = new PostgresOrderRepository(database.DataSource);
        _consumer = new KitchenEventOrderConsumer(database.DataSource, _orders, _logger);
    }

    [Fact]
    public void CanHandleOnlyItsOwnEventType()
    {
        _consumer.CanHandle(KitchenIntegrationEventTypes.KitchenTicketItemStateChanged).Should().BeTrue();
        _consumer.CanHandle("tables.table-merged.v1").Should().BeFalse();
    }

    [Fact]
    public async Task MirrorsTheStateOntoTheMatchingOrderItem()
    {
        var product = await SeedProduct();
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), product, "Lahmacun", 1, 120m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);
        await _orders.AddAsync(order);

        await DeliverAsync(order.Id, itemId, "Preparing");

        var reloaded = await _orders.GetByIdAsync(order.Id);
        reloaded!.Items.Single().KitchenState.Should().Be(KitchenState.Preparing);
        reloaded.RowVersion.Should().Be(2, "the mirror is a real write, so the row version advances");
    }

    [Fact]
    public async Task QueuedMapsOntoOrdersSentName()
    {
        var product = await SeedProduct();
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), product, "Lahmacun", 1, 120m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);
        await _orders.AddAsync(order);

        await DeliverAsync(order.Id, itemId, "Queued");

        (await _orders.GetByIdAsync(order.Id))!.Items.Single().KitchenState.Should().Be(KitchenState.Sent);
    }

    [Fact]
    public async Task RedeliveryOfTheSameStateDoesNotBumpTheRowVersionAgain()
    {
        var product = await SeedProduct();
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), product, "Lahmacun", 1, 120m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);
        await _orders.AddAsync(order);

        await DeliverAsync(order.Id, itemId, "Ready");
        var afterFirst = await _orders.GetByIdAsync(order.Id);

        await DeliverAsync(order.Id, itemId, "Ready");
        var afterRedelivery = await _orders.GetByIdAsync(order.Id);

        afterRedelivery!.RowVersion.Should().Be(afterFirst!.RowVersion, "a redelivery of an already-applied state is a no-op");
    }

    [Fact]
    public async Task AVoidedItemIgnoresALateArrivingKitchenEvent()
    {
        var product = await SeedProduct();
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), product, "Lahmacun", 1, 120m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]).CancelItem(itemId, reason: "customer changed mind");
        await _orders.AddAsync(order);

        await DeliverAsync(order.Id, itemId, "Preparing");

        var reloaded = await _orders.GetByIdAsync(order.Id);
        reloaded!.Items.Single().Status.Should().Be(OrderItemState.Cancelled);
        reloaded.Items.Single().KitchenState.Should().Be(KitchenState.Cancelled, "the void's own kitchen state must not be overwritten");
    }

    [Theory]
    [InlineData("Ready", "Queued", KitchenState.Ready)]
    [InlineData("Ready", "Preparing", KitchenState.Ready)]
    [InlineData("Served", "Ready", KitchenState.Served)]
    public async Task ALateEarlierStateNeverMovesTheItemBackwards(string first, string late, KitchenState expected)
    {
        var (order, itemId) = await SeedOrderAsync(KitchenState.NotSent);

        await DeliverAsync(order.Id, itemId, first);
        await DeliverAsync(order.Id, itemId, late);

        (await _orders.GetByIdAsync(order.Id))!.Items.Single().KitchenState.Should().Be(expected);
    }

    [Fact]
    public async Task AQueuedEventDoesNotReleaseAHeldCourseItem()
    {
        var (order, itemId) = await SeedOrderAsync(KitchenState.Held);

        await DeliverAsync(order.Id, itemId, "Queued");

        var reloaded = await _orders.GetByIdAsync(order.Id);
        reloaded!.Items.Single().KitchenState.Should().Be(KitchenState.Held);
        reloaded.RowVersion.Should().Be(order.RowVersion, "a stale event is not a write");
    }

    [Fact]
    public async Task ACancelledEventStillReachesAnItemTheKitchenAlreadyStarted()
    {
        var (order, itemId) = await SeedOrderAsync(KitchenState.NotSent);

        await DeliverAsync(order.Id, itemId, "Preparing");
        await DeliverAsync(order.Id, itemId, "Cancelled");

        (await _orders.GetByIdAsync(order.Id))!.Items.Single().KitchenState.Should().Be(KitchenState.Cancelled);
    }

    [Fact]
    public async Task EventsThatCannotBeMirroredAreLoggedWithTheirReason()
    {
        var (order, itemId) = await SeedOrderAsync(KitchenState.NotSent);

        await DeliverAsync(order.Id, itemId, "SomethingNew");
        await DeliverAsync(Guid.NewGuid(), itemId, "Ready");
        await DeliverAsync(order.Id, Guid.NewGuid(), "Ready");

        _logger.Warnings.Should().HaveCount(3);
        _logger.Warnings[0].Should().Contain("unknown item state 'SomethingNew'");
        _logger.Warnings[1].Should().Contain("order not found");
        _logger.Warnings[2].Should().Contain("order has no such item");
    }

    private async Task<(Order Order, Guid ItemId)> SeedOrderAsync(KitchenState kitchenState)
    {
        var product = await SeedProduct();
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), product, "Lahmacun", 1, 120m, 10m,
            status: OrderItemState.Active, kitchenState: kitchenState);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, UniqueNumber(), [item]);
        await _orders.AddAsync(order);
        return (order, itemId);
    }

    private sealed class CapturingLogger : ILogger<KitchenEventOrderConsumer>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }

    private async Task DeliverAsync(Guid orderId, Guid orderItemId, string itemState)
    {
        var payload = IntegrationEventSerializer.Serialize(
            new KitchenTicketItemStateChanged(Guid.NewGuid(), orderId, orderItemId, itemState, DateTimeOffset.UtcNow));
        await _consumer.HandleAsync(
            KitchenIntegrationEventTypes.KitchenTicketItemStateChanged, payload, CancellationToken.None);
    }

    private async Task<Guid> SeedProduct()
    {
        var productId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", "LAH-" + Guid.NewGuid().ToString("N")[..8]);
        command.Parameters.AddWithValue("name", "Lahmacun");
        command.Parameters.AddWithValue("product_type", 1);
        command.Parameters.AddWithValue("stock_mode", 1);
        command.Parameters.AddWithValue("current_price", 120m);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private static string UniqueNumber() => "ORD-" + Guid.NewGuid().ToString("N")[..8];
}
