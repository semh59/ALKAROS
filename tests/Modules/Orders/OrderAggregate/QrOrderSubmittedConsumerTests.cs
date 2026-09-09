namespace ALKAROS.Orders.OrderAggregate.Tests;

using ALKAROS.IntegrationContracts;
using ALKAROS.Orders.Integration;
using ALKAROS.Orders.OrderAggregate.Tests.Fixtures;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Tables.TableLifecycle;
using FluentAssertions;
using Npgsql;
using NpgsqlTypes;
using Xunit;

/// <summary>
/// V12-QRO-001: Order's reaction to a QR Ordering submission (V0-ARC-001 row
/// 19 — event-only, no direct-call edge), over the real repository and
/// database — same shape as KitchenEventOrderConsumerTests.
/// </summary>
public sealed class QrOrderSubmittedConsumerTests : IClassFixture<OrdersTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresTableRepository _tables;
    private readonly QrOrderSubmittedConsumer _consumer;

    public QrOrderSubmittedConsumerTests(OrdersTestDatabase database)
    {
        _dataSource = database.DataSource;
        _orders = new PostgresOrderRepository(database.DataSource);
        _tables = new PostgresTableRepository(database.DataSource);
        var submitHandler = new SubmitOrderHandler(database.DataSource, _orders);
        _consumer = new QrOrderSubmittedConsumer(database.DataSource, _orders, _tables, submitHandler);
    }

    [Fact]
    public void CanHandleOnlyItsOwnEventType()
    {
        _consumer.CanHandle(QrOrderingIntegrationEventTypes.QrOrderSubmitted).Should().BeTrue();
        _consumer.CanHandle("tables.table-merged.v1").Should().BeFalse();
    }

    [Fact]
    public async Task MaterializesAPendingConfirmationOrderFromTheEventsOwnSnapshot()
    {
        var tableId = await SeedTable();
        var submissionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var productId = await SeedProduct();

        await DeliverAsync(new QrOrderSubmitted(
            submissionId, tableId, Guid.NewGuid(),
            [new QrOrderSubmittedItem(itemId, productId, "Lahmacun", 2, 120m, 10m, "az acılı")],
            DateTimeOffset.UtcNow));

        var orderId = await FindOrderId(tableId, submissionId);
        orderId.Should().NotBeNull();

        var order = await _orders.GetByIdAsync(orderId!.Value);
        order.Should().NotBeNull();
        order!.Source.Should().Be(OrderSource.Qr);
        order.TableId.Should().Be(tableId);
        order.SourceReferenceId.Should().Be(submissionId);
        order.Status.Should().Be(OrderState.PendingConfirmation);
        var item = order.Items.Single();
        item.ProductId.Should().Be(productId);
        item.ProductNameSnapshot.Should().Be("Lahmacun");
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(120m);
        item.Notes.Should().Be("az acılı");
    }

    /// <summary>
    /// V12-QRO-002: the table's current_status is already Reserved by QR
    /// Ordering's own reservation policy at submission time (not this
    /// consumer's concern); this consumer only backfills the
    /// current_order_id cache pointer once the real Order exists.
    /// </summary>
    [Fact]
    public async Task BackfillsTheTablesCurrentOrderIdPointer()
    {
        var tableId = await SeedTable();
        var submissionId = Guid.NewGuid();
        var productId = await SeedProduct();

        await DeliverAsync(new QrOrderSubmitted(
            submissionId, tableId, Guid.NewGuid(),
            [new QrOrderSubmittedItem(Guid.NewGuid(), productId, "Lahmacun", 1, 120m, 10m, null)],
            DateTimeOffset.UtcNow));

        var orderId = await FindOrderId(tableId, submissionId);
        var currentOrderId = await GetTableCurrentOrderId(tableId);
        currentOrderId.Should().Be(orderId);
    }

    private async Task<Guid?> GetTableCurrentOrderId(Guid tableId)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        var result = await cmd.ExecuteScalarAsync();
        return result is Guid id ? id : null;
    }

    [Fact]
    public async Task RedeliveryOfTheSameSubmissionDoesNotCreateASecondOrder()
    {
        var tableId = await SeedTable();
        var productId = await SeedProduct();
        var submissionId = Guid.NewGuid();
        var @event = new QrOrderSubmitted(
            submissionId, tableId, Guid.NewGuid(),
            [new QrOrderSubmittedItem(Guid.NewGuid(), productId, "Ayran", 1, 25m, 1m, null)],
            DateTimeOffset.UtcNow);

        await DeliverAsync(@event);
        var firstOrderId = await FindOrderId(tableId, submissionId);

        await DeliverAsync(@event);
        var secondOrderId = await FindOrderId(tableId, submissionId);

        secondOrderId.Should().Be(firstOrderId);
        (await CountOrdersForTable(tableId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentRedeliveryLetsExactlyOneOrderWin()
    {
        var tableId = await SeedTable();
        var productId = await SeedProduct();
        var submissionId = Guid.NewGuid();
        var @event = new QrOrderSubmitted(
            submissionId, tableId, Guid.NewGuid(),
            [new QrOrderSubmittedItem(Guid.NewGuid(), productId, "Kola", 1, 40m, 10m, null)],
            DateTimeOffset.UtcNow);

        await Task.WhenAll(DeliverAsync(@event), DeliverAsync(@event));

        (await CountOrdersForTable(tableId)).Should().Be(1);
        var orderId = await FindOrderId(tableId, submissionId);
        var order = await _orders.GetByIdAsync(orderId!.Value);
        order!.Status.Should().Be(OrderState.PendingConfirmation);
    }

    private Task DeliverAsync(QrOrderSubmitted @event)
    {
        var payload = IntegrationEventSerializer.Serialize(@event);
        return _consumer.HandleAsync(QrOrderingIntegrationEventTypes.QrOrderSubmitted, payload, CancellationToken.None);
    }

    private async Task<Guid?> FindOrderId(Guid tableId, Guid submissionId)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT order_id FROM orders.orders WHERE table_id = @table_id AND source_reference_id = @submission_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        var result = await cmd.ExecuteScalarAsync();
        return result is Guid id ? id : null;
    }

    private async Task<long> CountOrdersForTable(Guid tableId)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT count(*) FROM orders.orders WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        return (long)(await cmd.ExecuteScalarAsync())!;
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
        command.Parameters.AddWithValue("sku", "QR-" + Guid.NewGuid().ToString("N")[..8]);
        command.Parameters.AddWithValue("name", "Lahmacun");
        command.Parameters.AddWithValue("product_type", 1);
        command.Parameters.AddWithValue("stock_mode", 1);
        command.Parameters.AddWithValue("current_price", 120m);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private async Task<Guid> SeedTable()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await using var cmd = _dataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, 'Available');
            """);
        cmd.Parameters.Add("zone_id", NpgsqlDbType.Uuid).Value = zoneId;
        cmd.Parameters.Add("zone_code", NpgsqlDbType.Text).Value = "ZONE-" + zoneId.ToString("N")[..8];
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("table_number", NpgsqlDbType.Text).Value = "T-" + tableId.ToString("N")[..6];
        await cmd.ExecuteNonQueryAsync();
        return tableId;
    }
}
