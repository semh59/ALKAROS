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
            IsManagerAuthorized: true,
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
            IsManagerAuthorized: true,
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
            IsManagerAuthorized: true,
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
            IsManagerAuthorized: true,
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
    public async Task VoidItemAsyncFailsClosedWhenNotManagerAuthorized()
    {
        var (order, item1, _) = await CreateAndSeedSubmittedOrderAsync(KitchenState.NotSent);
        var waiterId = Guid.NewGuid();

        var cmd = new VoidOrderItemCommand(
            order.Id,
            item1.Id,
            order.RowVersion,
            waiterId,
            IsManagerAuthorized: false,
            ReasonCode: VoidReasonCatalog.OperatorError,
            CorrelationId: "corr-unauth-void");

        var act = () => _handler.VoidItemAsync(cmd);

        await act.Should().ThrowAsync<UnauthorizedItemOperationException>()
            .WithMessage("*Manager authority is required*");
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
            IsManagerAuthorized: true,
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
    public async Task ApplyComplimentaryAsyncFailsClosedWhenNotManagerAuthorized()
    {
        var (order, item1, _) = await CreateAndSeedSubmittedOrderAsync(KitchenState.NotSent);
        var actorId = Guid.NewGuid();

        var cmd = new ApplyComplimentaryCommand(
            order.Id,
            item1.Id,
            order.RowVersion,
            actorId,
            IsManagerAuthorized: false,
            ReasonCode: ComplimentaryReasonCatalog.VIPGuest,
            CorrelationId: "corr-unauth-comp");

        var act = () => _handler.ApplyComplimentaryAsync(cmd);

        await act.Should().ThrowAsync<UnauthorizedItemOperationException>();
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
            IsManagerAuthorized: true,
            ReasonCode: VoidReasonCatalog.OperatorError,
            CorrelationId: "corr-stale");

        var act = () => _handler.VoidItemAsync(cmd);

        await act.Should().ThrowAsync<StaleOrderRowVersionException>();
    }
}
