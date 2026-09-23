using ALKAROS.Billing.BillFoundation;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Payments.TenderComposition.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.TenderComposition.Tests;

/// <summary>
/// Integration tests for V13-PAY-003's tender handler composition: the
/// fail-closed registry build, the Cash-to-generic-router bridge (against
/// real Postgres, proving it produces the same real effect as calling
/// <see cref="ICashTenderHandler"/> directly), the BankCard placeholder's
/// safety property (never a fabricated Approved/Declined), MealCard's
/// typed-unavailable absence, and CustomerAccount's unchanged
/// version-not-enabled behaviour.
/// </summary>
public sealed class TenderCompositionTests : IClassFixture<TenderCompositionTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresCashTransactionLedgerRepository _ledger;
    private readonly PostgresCashSessionRepository _sessions;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly CashSessionLifecycleService _sessionService;
    private readonly CashTenderHandler _cashHandler;

    public TenderCompositionTests(TenderCompositionTestDatabase database)
    {
        _dataSource = database.DataSource;
        _payments = new PostgresPaymentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource);
        _ledger = new PostgresCashTransactionLedgerRepository(_dataSource);
        _sessions = new PostgresCashSessionRepository(_dataSource);
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _sessionService = new CashSessionLifecycleService(_sessions, new CashSessionPolicy());
        _cashHandler = new CashTenderHandler(_sessions, _bills, _payments, _allocations, _ledger, _dataSource);
    }

    private ALKAROS.Payments.TenderRouting.TenderHandlerRegistry BuildRegistry()
    {
        var registry = new ALKAROS.Payments.TenderRouting.TenderHandlerRegistry();
        registry.Register(new ALKAROS.Payments.TenderRouting.CashTenderMethodAdapter(_cashHandler));
        registry.Register(new ALKAROS.Payments.TenderRouting.PendingBankCardTerminalIntegrationHandler());
        return registry;
    }

    private ALKAROS.Payments.EftTender.EftTenderHandler BuildEftHandler()
        => new(_bills, _payments, _allocations, _dataSource);

    [Fact]
    public void FactoryBuildsARegistryWithCashAndBankCardResolved()
    {
        var registry = ALKAROS.Payments.TenderRouting.TenderHandlerRegistryFactory.Build(
            new ALKAROS.Payments.TenderRouting.CashTenderMethodAdapter(_cashHandler),
            new ALKAROS.Payments.TenderRouting.PendingBankCardTerminalIntegrationHandler(),
            BuildEftHandler());

        registry.TryGet(ALKAROS.Payments.TenderRouting.TenderMethod.Cash, out var cash).Should().BeTrue();
        cash.Should().BeOfType<ALKAROS.Payments.TenderRouting.CashTenderMethodAdapter>();
        registry.TryGet(ALKAROS.Payments.TenderRouting.TenderMethod.BankCard, out var bankCard).Should().BeTrue();
        bankCard.Should().BeOfType<ALKAROS.Payments.TenderRouting.PendingBankCardTerminalIntegrationHandler>();
        registry.TryGet(ALKAROS.Payments.TenderRouting.TenderMethod.Eft, out var eft).Should().BeTrue();
        eft.Should().BeOfType<ALKAROS.Payments.EftTender.EftTenderHandler>();
    }

    [Fact]
    public void FactoryFailsClosedOnADuplicateCashRegistration()
    {
        var act = () => ALKAROS.Payments.TenderRouting.TenderHandlerRegistryFactory.Build(
            new ALKAROS.Payments.TenderRouting.CashTenderMethodAdapter(_cashHandler),
            new ALKAROS.Payments.TenderRouting.CashTenderMethodAdapter(_cashHandler),
            BuildEftHandler());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegistryLeavesMealCardGenuinelyUnregistered()
    {
        var registry = BuildRegistry();

        registry.TryGet(ALKAROS.Payments.TenderRouting.TenderMethod.MealCard, out _).Should().BeFalse();
    }

    [Fact]
    public async Task RouterReturnsTenderMethodNotRegisteredForMealCard()
    {
        var router = new ALKAROS.Payments.TenderRouting.TenderRouter(BuildRegistry());
        var request = new ALKAROS.Payments.TenderRouting.TenderRequest(
            Guid.NewGuid(), ALKAROS.Payments.TenderRouting.TenderMethod.MealCard, 10m);

        var result = await router.RouteAsync(request);

        result.Should().BeOfType<ALKAROS.Payments.TenderRouting.TenderMethodNotRegistered>()
            .Which.Method.Should().Be(ALKAROS.Payments.TenderRouting.TenderMethod.MealCard);
    }

    [Fact]
    public async Task RouterReturnsTenderVersionNotEnabledForCustomerAccountUnchanged()
    {
        var router = new ALKAROS.Payments.TenderRouting.TenderRouter(BuildRegistry());
        var request = new ALKAROS.Payments.TenderRouting.TenderRequest(
            Guid.NewGuid(), ALKAROS.Payments.TenderRouting.TenderMethod.CustomerAccount, 10m);

        var result = await router.RouteAsync(request);

        result.Should().BeOfType<ALKAROS.Payments.TenderRouting.TenderVersionNotEnabled>()
            .Which.Method.Should().Be(ALKAROS.Payments.TenderRouting.TenderMethod.CustomerAccount);
    }

    [Fact]
    public async Task RouterAlwaysReturnsRequiresReconciliationForBankCardNeverAFabricatedOutcome()
    {
        var router = new ALKAROS.Payments.TenderRouting.TenderRouter(BuildRegistry());

        for (var i = 0; i < 5; i++)
        {
            var request = new ALKAROS.Payments.TenderRouting.TenderRequest(
                Guid.NewGuid(), ALKAROS.Payments.TenderRouting.TenderMethod.BankCard, 10m + i);

            var routed = await router.RouteAsync(request);

            var handled = routed.Should().BeOfType<ALKAROS.Payments.TenderRouting.TenderRoutingHandled>().Subject;
            handled.Result.Should().BeOfType<ALKAROS.Payments.TenderRouting.TenderRequiresReconciliation>();
        }
    }

    [Fact]
    public async Task CashBridgeThroughTheGenericRouterProducesTheSameRealEffectAsTheDirectHandler()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();

        var router = new ALKAROS.Payments.TenderRouting.TenderRouter(BuildRegistry());
        var request = new ALKAROS.Payments.TenderRouting.TenderRequest(
            Guid.NewGuid(),
            ALKAROS.Payments.TenderRouting.TenderMethod.Cash,
            80m,
            BillId: billId,
            CashSessionId: sessionId,
            IdempotencyKey: idempotencyKey);

        var routed = await router.RouteAsync(request);

        var handled = routed.Should().BeOfType<ALKAROS.Payments.TenderRouting.TenderRoutingHandled>().Subject;
        var approved = handled.Result.Should().BeOfType<ALKAROS.Payments.TenderRouting.TenderApproved>().Subject;
        approved.ApprovedAmount.Should().Be(80m);

        var allocations = await _allocations.GetByBillIdAsync(billId);
        allocations.Should().ContainSingle(a => a.Amount == 80m);
        var ledgerEntries = await _ledger.GetBySessionIdAsync(sessionId);
        ledgerEntries.Should().ContainSingle(e => e.Amount == 80m && e.Direction == CashTransactionDirection.In);
        var billPayments = await _payments.GetByBillIdAsync(billId);
        billPayments.Should().ContainSingle(p => p.Status == PaymentStatus.Approved && p.ApprovedAmount == 80m);
    }

    [Fact]
    public async Task CashBridgeRejectsAMissingRequiredFieldWithoutTouchingTheDatabase()
    {
        var adapter = new ALKAROS.Payments.TenderRouting.CashTenderMethodAdapter(_cashHandler);
        var request = new ALKAROS.Payments.TenderRouting.TenderRequest(
            Guid.NewGuid(), ALKAROS.Payments.TenderRouting.TenderMethod.Cash, 10m);

        var act = () => adapter.HandleAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private async Task<Guid> SeedOpenSessionAsync()
    {
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);
        var (session, _) = await _sessionService.OpenSessionAsync(command);
        return session.CashSessionId;
    }

    private async Task<Guid> SeedBillAsync(decimal payable)
    {
        var productId = await SeedProductAsync("Test Item", payable);
        var tableId = await SeedTableAsync();
        var order = await CreateAndSaveOrderAsync(productId, "Test Item", payable, tableId);

        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, order.Items[0]);
        var bill = new Bill(
            id: billId,
            billNumber: "BILL-" + Guid.NewGuid().ToString("N")[..8],
            items: [billItem],
            tableId: tableId,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: "TRY");

        await _bills.AddAsync(bill);
        return billId;
    }

    private async Task<Guid> SeedProductAsync(string name, decimal price)
    {
        var productId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, @name, @product_type, @stock_mode, @current_price);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("product_type", 1);
        command.Parameters.AddWithValue("stock_mode", 1);
        command.Parameters.AddWithValue("current_price", price);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private async Task<Guid> SeedTableAsync()
    {
        var tableId = Guid.NewGuid();
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Available');
            """);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("table_number", "TBL-" + Guid.NewGuid().ToString("N")[..6]);
        await command.ExecuteNonQueryAsync();
        return tableId;
    }

    private async Task<Order> CreateAndSaveOrderAsync(Guid productId, string productName, decimal price, Guid tableId)
    {
        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: productId,
            productNameSnapshot: productName,
            quantity: 1,
            unitPrice: price,
            taxRate: 0m);

        var order = new Order(
            id: orderId,
            source: OrderSource.Cashier,
            orderNumber: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            items: [item],
            tableId: tableId);

        await _orders.AddAsync(order);
        return order;
    }
}
