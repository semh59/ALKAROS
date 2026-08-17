namespace ALKAROS.Orders.SubmitOrder.Tests;

using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class SubmitOrderTestDatabase : PgTestDatabase
{
    public SubmitOrderTestDatabase()
        : base("alkaros_ord002_")
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

public sealed class SubmitOrderUnitTests
{
    [Fact]
    public void ValidateThrowsOnEmptyClientId()
    {
        var cmd = new SubmitOrderCommand("", "op-1", Guid.NewGuid(), 1);
        var act = () => cmd.Validate();
        act.Should().Throw<ArgumentException>().WithParameterName("ClientId");
    }

    [Fact]
    public void ValidateThrowsOnEmptyOperationId()
    {
        var cmd = new SubmitOrderCommand("client-1", "", Guid.NewGuid(), 1);
        var act = () => cmd.Validate();
        act.Should().Throw<ArgumentException>().WithParameterName("OperationId");
    }

    [Fact]
    public void ValidateThrowsOnEmptyOrderId()
    {
        var cmd = new SubmitOrderCommand("client-1", "op-1", Guid.Empty, 1);
        var act = () => cmd.Validate();
        act.Should().Throw<ArgumentException>().WithParameterName("OrderId");
    }

    [Fact]
    public void ValidateThrowsOnInvalidRowVersion()
    {
        var cmd = new SubmitOrderCommand("client-1", "op-1", Guid.NewGuid(), 0);
        var act = () => cmd.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("ExpectedRowVersion");
    }

    [Fact]
    public void RequestHashIsDeterministic()
    {
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var cmd1 = new SubmitOrderCommand("client-1", "op-1", orderId, 1, Reason: "submit", SubmittedAt: now);
        var cmd2 = new SubmitOrderCommand("client-1", "op-1", orderId, 1, Reason: "submit", SubmittedAt: now);

        SubmitOrderRequestHash.Compute(cmd1).Should().Be(SubmitOrderRequestHash.Compute(cmd2));
    }

    [Fact]
    public void RequestHashDiffersOnModifiedPayload()
    {
        var orderId = Guid.NewGuid();
        var cmd1 = new SubmitOrderCommand("client-1", "op-1", orderId, 1, Reason: "reason1");
        var cmd2 = new SubmitOrderCommand("client-1", "op-1", orderId, 1, Reason: "reason2");

        SubmitOrderRequestHash.Compute(cmd1).Should().NotBe(SubmitOrderRequestHash.Compute(cmd2));
    }

    [Fact]
    public void SerializationRoundTripPreservesData()
    {
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var original = new SubmitOrderResult(orderId, "ORD-100", OrderState.Submitted, 2, now, 150.50m, 3, IsReplay: false);

        var bytes = SubmitOrderResponseSerializer.Serialize(original);
        var deserialized = SubmitOrderResponseSerializer.Deserialize(bytes, isReplay: true);

        deserialized.OrderId.Should().Be(orderId);
        deserialized.OrderNumber.Should().Be("ORD-100");
        deserialized.Status.Should().Be(OrderState.Submitted);
        deserialized.RowVersion.Should().Be(2);
        deserialized.Total.Should().Be(150.50m);
        deserialized.ItemCount.Should().Be(3);
        deserialized.IsReplay.Should().BeTrue();
    }
}

public sealed class PostgresSubmitOrderIntegrationTests : IClassFixture<SubmitOrderTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresOrderRepository _repository;
    private readonly SubmitOrderHandler _handler;

    public PostgresSubmitOrderIntegrationTests(SubmitOrderTestDatabase database)
    {
        _dataSource = database.DataSource;
        _repository = new PostgresOrderRepository(database.DataSource);
        _handler = new SubmitOrderHandler(database.DataSource, _repository);
    }

    private async Task<Guid> SeedProductAsync()
    {
        var productId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        command.Parameters.AddWithValue("name", "Burger");
        command.Parameters.AddWithValue("product_type", 1);
        command.Parameters.AddWithValue("stock_mode", 1);
        command.Parameters.AddWithValue("current_price", 120m);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private async Task<Order> CreateSampleDraftOrderAsync()
    {
        var productId = await SeedProductAsync();
        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            Guid.NewGuid(),
            orderId,
            productId,
            "Burger",
            2,
            120.00m,
            10.00m,
            skuSnapshot: "BURGER-01");

        var order = new Order(
            orderId,
            OrderSource.Waiter,
            "ORD-" + Guid.NewGuid().ToString("N")[..8],
            [item]);

        await _repository.AddAsync(order);
        return order;
    }

    [Fact]
    public async Task HandleAsyncSubmitsDraftOrderSuccessfully()
    {
        var order = await CreateSampleDraftOrderAsync();
        var cmd = new SubmitOrderCommand(
            "waiter-pwa-01",
            "op-submit-01",
            order.Id,
            order.RowVersion,
            Reason: "Customer confirmed order");

        var result = await _handler.HandleAsync(cmd);

        result.OrderId.Should().Be(order.Id);
        result.Status.Should().Be(OrderState.Submitted);
        result.RowVersion.Should().Be(order.RowVersion + 1);
        result.IsReplay.Should().BeFalse();
        result.Total.Should().Be(order.Total);

        var reloaded = await _repository.GetByIdAsync(order.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Status.Should().Be(OrderState.Submitted);
        reloaded.RowVersion.Should().Be(result.RowVersion);
    }

    [Fact]
    public async Task HandleAsyncReplayReturnsExactCachedResponseWithIsReplayTrue()
    {
        var order = await CreateSampleDraftOrderAsync();
        var cmd = new SubmitOrderCommand(
            "waiter-pwa-01",
            "op-submit-02",
            order.Id,
            order.RowVersion);

        var first = await _handler.HandleAsync(cmd);
        first.IsReplay.Should().BeFalse();

        // Second call with same client & operation id
        var second = await _handler.HandleAsync(cmd);
        second.IsReplay.Should().BeTrue();
        second.OrderId.Should().Be(first.OrderId);
        second.OrderNumber.Should().Be(first.OrderNumber);
        second.RowVersion.Should().Be(first.RowVersion);
        second.Status.Should().Be(OrderState.Submitted);
        second.Total.Should().Be(first.Total);

        // Verify order on DB was NOT updated a second time
        var reloaded = await _repository.GetByIdAsync(order.Id);
        reloaded!.RowVersion.Should().Be(first.RowVersion);
    }

    [Fact]
    public async Task HandleAsyncReusedKeyWithModifiedPayloadThrowsConflictException()
    {
        var order1 = await CreateSampleDraftOrderAsync();
        var order2 = await CreateSampleDraftOrderAsync();

        var cmd1 = new SubmitOrderCommand("waiter-pwa-01", "op-reused-key", order1.Id, order1.RowVersion);
        var first = await _handler.HandleAsync(cmd1);
        first.IsReplay.Should().BeFalse();

        // Same ClientId & OperationId, but different OrderId
        var cmd2 = new SubmitOrderCommand("waiter-pwa-01", "op-reused-key", order2.Id, order2.RowVersion);
        var act = () => _handler.HandleAsync(cmd2);

        await act.Should().ThrowAsync<SubmitOrderIdempotencyConflictException>();
    }

    [Fact]
    public async Task HandleAsyncStaleVersionThrowsStaleOrderVersionException()
    {
        var order = await CreateSampleDraftOrderAsync();
        var staleVersion = order.RowVersion + 99;

        var cmd = new SubmitOrderCommand("waiter-pwa-01", "op-stale-01", order.Id, staleVersion);
        var act = () => _handler.HandleAsync(cmd);

        await act.Should().ThrowAsync<StaleOrderVersionException>();

        // Order remains in Draft state
        var reloaded = await _repository.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderState.Draft);
    }

    [Fact]
    public async Task HandleAsyncOrderNotFoundThrowsOrderNotFoundException()
    {
        var nonExistentId = Guid.NewGuid();
        var cmd = new SubmitOrderCommand("waiter-pwa-01", "op-404-01", nonExistentId, 1);
        var act = () => _handler.HandleAsync(cmd);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public async Task HandleAsyncInvalidTransitionWhenOrderNotInDraftStateThrowsInvalidOperationException()
    {
        var order = await CreateSampleDraftOrderAsync();

        // First transition to Submitted
        var submitted = order.TransitionTo(OrderState.Submitted);
        var newVersion = await _repository.SaveAsync(submitted, order.RowVersion);

        // Try submitting again with fresh idempotency key
        var cmd = new SubmitOrderCommand("waiter-pwa-01", "op-already-submitted", order.Id, newVersion);
        var act = () => _handler.HandleAsync(cmd);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot transition from Submitted to Submitted*");
    }

    [Fact]
    public async Task HandleAsyncConcurrentSubmissionsWithSameKeySucceedsDeterministically()
    {
        var order = await CreateSampleDraftOrderAsync();
        var cmd = new SubmitOrderCommand("waiter-pwa-01", "op-concurrent-01", order.Id, order.RowVersion);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _handler.HandleAsync(cmd))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All 5 returned results have exact same order id, status, row version
        results.Should().AllSatisfy(r =>
        {
            r.OrderId.Should().Be(order.Id);
            r.Status.Should().Be(OrderState.Submitted);
            r.RowVersion.Should().Be(order.RowVersion + 1);
        });

        // Exactly one created the record, other 4 received replayed response
        results.Count(r => !r.IsReplay).Should().Be(1);
        results.Count(r => r.IsReplay).Should().Be(4);
    }
}
