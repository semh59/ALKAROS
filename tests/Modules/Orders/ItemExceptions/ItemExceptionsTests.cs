namespace ALKAROS.Orders.ItemExceptions.Tests;

using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class ItemExceptionsTestDatabase : PgTestDatabase
{
    public ItemExceptionsTestDatabase()
        : base("alkaros_ord003_")
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

public sealed class ReasonCatalogUnitTests
{
    [Theory]
    [InlineData("OperatorError")]
    [InlineData("ProductUnavailable")]
    [InlineData("CustomerChange")]
    [InlineData("DuplicateEntry")]
    public void VoidCatalogAcceptsRecognizedReasons(string reason)
    {
        VoidReasonCatalog.IsValid(reason).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Customer didn't like food")]
    [InlineData("Random reason")]
    public void VoidCatalogRejectsUnrecognizedReasons(string reason)
    {
        VoidReasonCatalog.IsValid(reason).Should().BeFalse();
    }

    [Theory]
    [InlineData("CustomerSatisfaction")]
    [InlineData("ManagerPromotion")]
    [InlineData("VIPGuest")]
    [InlineData("ServiceApology")]
    public void ComplimentaryCatalogAcceptsRecognizedReasons(string reason)
    {
        ComplimentaryReasonCatalog.IsValid(reason).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Free lunch")]
    [InlineData("Friend of chef")]
    public void ComplimentaryCatalogRejectsUnrecognizedReasons(string reason)
    {
        ComplimentaryReasonCatalog.IsValid(reason).Should().BeFalse();
    }
}

public sealed class ItemExceptionCommandUnitTests
{
    [Fact]
    public void VoidCommandValidateThrowsOnUnrecognizedReason()
    {
        var cmd = new VoidOrderItemCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            ReasonCode: "NotARealReason",
            CorrelationId: "corr-1");

        var act = () => cmd.Validate();
        act.Should().Throw<InvalidItemReasonException>();
    }

    [Fact]
    public void ComplimentaryCommandValidateThrowsOnUnrecognizedReason()
    {
        var cmd = new ApplyComplimentaryCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            ReasonCode: "InvalidCompReason",
            CorrelationId: "corr-2");

        var act = () => cmd.Validate();
        act.Should().Throw<InvalidItemReasonException>();
    }
}

public sealed class PostgresItemExceptionsIntegrationTests : IClassFixture<ItemExceptionsTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresOrderRepository _orderRepo;
    private readonly ItemExceptionHandler _handler;

    public PostgresItemExceptionsIntegrationTests(ItemExceptionsTestDatabase database)
    {
        _dataSource = database.DataSource;
        _orderRepo = new PostgresOrderRepository(database.DataSource);
        _handler = new ItemExceptionHandler(_orderRepo, database.DataSource);
    }

    private async Task<(Order Order, OrderItem BurgerItem, OrderItem FriesItem)> CreateAndSeedSubmittedOrderAsync(
        KitchenState item1KitchenState = KitchenState.NotSent)
    {
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var sku1 = "BURGER-" + Guid.NewGuid().ToString("N")[..8];
        var sku2 = "FRIES-" + Guid.NewGuid().ToString("N")[..8];

        await using (var cmd = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@p1, @sku1, 'Burger', 1, 1, 150.00);

            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@p2, @sku2, 'Fries', 1, 1, 50.00);
            """))
        {
            cmd.Parameters.AddWithValue("p1", productId1);
            cmd.Parameters.AddWithValue("sku1", sku1);
            cmd.Parameters.AddWithValue("p2", productId2);
            cmd.Parameters.AddWithValue("sku2", sku2);
            await cmd.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item1 = new OrderItem(
            Guid.NewGuid(),
            orderId,
            productId1,
            "Burger",
            2,
            150.00m,
            10.00m,
            sku1,
            status: OrderItemState.Active,
            kitchenState: item1KitchenState);

        var item2 = new OrderItem(
            Guid.NewGuid(),
            orderId,
            productId2,
            "Fries",
            1,
            50.00m,
            10.00m,
            sku2,
            status: OrderItemState.Active,
            kitchenState: KitchenState.NotSent);

        var order = new Order(
            orderId,
            OrderSource.Waiter,
            "ORD-" + Guid.NewGuid().ToString("N")[..8],
            [item1, item2],
            status: OrderState.Submitted);

        await _orderRepo.AddAsync(order);
        return (order, item1, item2);
    }

    [Fact]
    public async Task VoidItemAsyncSucceedsForPreKitchenActiveItemWithManagerApproval()
    {
        var (order, item1, item2) = await CreateAndSeedSubmittedOrderAsync(KitchenState.NotSent);
        var managerId = Guid.NewGuid();

        var cmd = new VoidOrderItemCommand(
            order.Id,
            item1.Id,
            order.RowVersion,
            managerId,
            ReasonCode: VoidReasonCatalog.CustomerChange,
            CorrelationId: "corr-void-1",
            Notes: "Customer changed to salad");

        var result = await _handler.VoidItemAsync(cmd);

        result.OrderId.Should().Be(order.Id);
        result.OrderItemId.Should().Be(item1.Id);
        result.NewItemStatus.Should().Be(OrderItemState.Cancelled);
        result.NewOrderRowVersion.Should().Be(order.RowVersion + 1);

        // Subtotal recalculation: only item2 (Fries = 50.00) remains active
        result.NewOrderTotal.Should().Be(item2.GrossAmount);

        // Verify DB persistence
        var reloaded = await _orderRepo.GetByIdAsync(order.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Items.First(i => i.Id == item1.Id).Status.Should().Be(OrderItemState.Cancelled);
        reloaded.Items.First(i => i.Id == item2.Id).Status.Should().Be(OrderItemState.Active);
        reloaded.History.Should().Contain(h => h.Reason!.Contains("CustomerChange"));
    }

    [Fact]
    public async Task VoidItemAsyncFailsClosedWhenKitchenPreparationHasBegun()
    {
        // Item 1 has already progressed in kitchen to Preparing
        var (order, item1, _) = await CreateAndSeedSubmittedOrderAsync(KitchenState.Preparing);
        var managerId = Guid.NewGuid();

        var cmd = new VoidOrderItemCommand(
            order.Id,
            item1.Id,
            order.RowVersion,
            managerId,
            ReasonCode: VoidReasonCatalog.CustomerChange,
            CorrelationId: "corr-late-void");

        var act = () => _handler.VoidItemAsync(cmd);

        await act.Should().ThrowAsync<LateVoidRejectedException>()
            .WithMessage("*cannot be voided because kitchen preparation has already progressed*");

        // Verify order remains unchanged on DB
        var reloaded = await _orderRepo.GetByIdAsync(order.Id);
        reloaded!.Items.First(i => i.Id == item1.Id).Status.Should().Be(OrderItemState.Active);
    }

    [Fact]
    public async Task ApplyComplimentaryAsyncReducesTotalToZeroWhilePreservingQuantityAndTaxSnapshots()
    {
        var (order, item1, item2) = await CreateAndSeedSubmittedOrderAsync(KitchenState.Preparing);
        var managerId = Guid.NewGuid();

        var cmd = new ApplyComplimentaryCommand(
            order.Id,
            item1.Id,
            order.RowVersion,
            managerId,
            ReasonCode: ComplimentaryReasonCatalog.ServiceApology,
            CorrelationId: "corr-comp-1",
            Notes: "Complimentary burger due to 30 min kitchen delay");

        var result = await _handler.ApplyComplimentaryAsync(cmd);

        result.OrderId.Should().Be(order.Id);
        result.OrderItemId.Should().Be(item1.Id);
        result.NewItemStatus.Should().Be(OrderItemState.Complimentary);
        result.NewOrderRowVersion.Should().Be(order.RowVersion + 1);

        // Reload from DB and check invariant
        var reloaded = await _orderRepo.GetByIdAsync(order.Id);
        reloaded.Should().NotBeNull();

        var compItem = reloaded!.Items.First(i => i.Id == item1.Id);
        compItem.Status.Should().Be(OrderItemState.Complimentary);
        compItem.Quantity.Should().Be(2);
        compItem.UnitPrice.Should().Be(150.00m); // Snapshot preserved
        compItem.TaxRate.Should().Be(10.00m);   // Snapshot preserved
        compItem.GrossAmount.Should().Be(0m);   // Payable amount is 0

        // Only item2 is charged to the order total
        reloaded.Total.Should().Be(item2.GrossAmount);
        reloaded.History.Should().Contain(h => h.Reason!.Contains("ServiceApology"));
    }

    [Fact]
    public async Task OperationFailsOnStaleRowVersion()
    {
        var (order, item1, _) = await CreateAndSeedSubmittedOrderAsync(KitchenState.NotSent);
        var managerId = Guid.NewGuid();

        var cmd = new VoidOrderItemCommand(
            order.Id,
            item1.Id,
            order.RowVersion + 99,
            managerId,
            ReasonCode: VoidReasonCatalog.OperatorError,
            CorrelationId: "corr-stale");

        var act = () => _handler.VoidItemAsync(cmd);

        await act.Should().ThrowAsync<StaleOrderRowVersionException>();
    }

    private async Task<long> AuditCountAsync(Guid orderId, string eventName)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @id AND event_name = @name;");
        cmd.Parameters.AddWithValue("id", orderId);
        cmd.Parameters.AddWithValue("name", eventName);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // V1-RMD-332 (independent 2026-09-26 audit, orta seviye bulgu): the domain write and its
    // own audit row used to go through two separate connections/commits — this proves the
    // successful path really does write both, not just the order.
    [Fact]
    public async Task VoidItemAsyncWritesExactlyOneAuditRowForTheSuccessfulVoid()
    {
        var (order, item1, _) = await CreateAndSeedSubmittedOrderAsync(KitchenState.NotSent);
        var cmd = new VoidOrderItemCommand(
            order.Id, item1.Id, order.RowVersion, Guid.NewGuid(),
            ReasonCode: VoidReasonCatalog.CustomerChange, CorrelationId: "corr-audit-void");

        await _handler.VoidItemAsync(cmd);

        (await AuditCountAsync(order.Id, "Order.ItemVoided")).Should().Be(1);
    }

    // Proves the fix is real, not cosmetic: when the audit insert itself fails (here, a
    // correlation id past the audit table's own VARCHAR(128) column - a real Postgres
    // constraint, not an injected fault), the domain write inside the SAME transaction rolls
    // back too. Before V1-RMD-332 this would have left the item permanently Cancelled with
    // no audit trail at all - the exact "silent gap" the finding named.
    [Fact]
    public async Task WhenTheAuditInsertItselfFailsTheDomainWriteInsideTheSameTransactionIsRolledBackToo()
    {
        var (order, item1, _) = await CreateAndSeedSubmittedOrderAsync(KitchenState.NotSent);
        var oversizedCorrelationId = new string('x', 200);
        var cmd = new VoidOrderItemCommand(
            order.Id, item1.Id, order.RowVersion, Guid.NewGuid(),
            ReasonCode: VoidReasonCatalog.CustomerChange, CorrelationId: oversizedCorrelationId);

        var act = () => _handler.VoidItemAsync(cmd);

        await act.Should().ThrowAsync<PostgresException>();
        var reloaded = await _orderRepo.GetByIdAsync(order.Id);
        reloaded!.Items.First(i => i.Id == item1.Id).Status.Should().Be(OrderItemState.Active);
        reloaded.RowVersion.Should().Be(order.RowVersion);
        (await AuditCountAsync(order.Id, "Order.ItemVoided")).Should().Be(0);
    }
}
