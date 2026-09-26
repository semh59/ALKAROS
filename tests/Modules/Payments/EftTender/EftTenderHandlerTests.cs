using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.EftTender.Tests.Fixtures;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Payments.TenderRouting;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.EftTender.Tests;

/// <summary>
/// Integration tests for <see cref="EftTenderHandler"/> against real
/// Postgres (V13-PAY-005). Exercises successful settlement with no change
/// amount, over-tender rejection (never a change calculation, unlike Cash),
/// idempotent replay, the optional free-text note, and — the single most
/// safety-critical property this task adds — total independence from
/// CashSession (this test database applies no cash_sessions/cash_transactions
/// migration at all, so any accidental coupling would fail to even compile
/// or would 42P01 at runtime, not just fail an assertion).
/// </summary>
public sealed class EftTenderHandlerTests : IClassFixture<EftTenderHandlerTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresBillAdjustmentRepository _adjustments;
    private readonly EftTenderHandler _handler;

    public EftTenderHandlerTests(EftTenderHandlerTestDatabase database)
    {
        _dataSource = database.DataSource;
        _payments = new PostgresPaymentRepository(_dataSource);
        _adjustments = new PostgresBillAdjustmentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource, _adjustments);
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _handler = new EftTenderHandler(_bills, _payments, _allocations, _adjustments, _dataSource);
    }

    /// <summary>
    /// V1-RMD-298 (independent 2026-09-26 audit, finding K1): the same tip-raises-the-real-ceiling fix as
    /// CashTenderHandlerTests' own equivalent test - before this fix, EftTenderHandler's own fail-fast
    /// pre-check read the Bill's never-updated PayableAmount directly and would reject this EFT tender.
    /// </summary>
    [Fact]
    public async Task HandleAsyncAcceptsATenderThatIncludesATipEvenThoughItExceedsTheOriginalPayable()
    {
        var billId = await SeedBillAsync(payable: 80m);
        await _adjustments.AddAsync(BillAdjustment.CreateTip(
            Guid.NewGuid(), billId, amount: 20m, reason: "Test tip", authorizedBy: Guid.NewGuid()));
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 100m,
            BillId: billId, IdempotencyKey: Guid.NewGuid().ToString());

        var result = await _handler.HandleAsync(request);

        ((TenderApproved)result).ApprovedAmount.Should().Be(100m);
        (await _allocations.GetByBillIdAsync(billId)).Should().ContainSingle(a => a.Amount == 100m);
    }

    [Fact]
    public void HandlerNeverTakesACashSessionDependency()
    {
        // Compile-time/reflection proof, not just behavioural: this
        // handler's constructor has no ICashSessionRepository (or any
        // ALKAROS.Cash.* type) parameter at all — the EftTender folder
        // doesn't even reference the Cash module's assembly.
        var constructor = typeof(EftTenderHandler).GetConstructors().Single();
        constructor.GetParameters()
            .Should().NotContain(p => p.ParameterType.FullName!.StartsWith("ALKAROS.Cash", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsyncSettlesTheFullAmountWithNoChange()
    {
        var billId = await SeedBillAsync(payable: 100m);
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 100m,
            BillId: billId, IdempotencyKey: Guid.NewGuid().ToString(), RecordedBy: Guid.NewGuid());

        var result = await _handler.HandleAsync(request);

        result.Should().BeOfType<TenderApproved>();
        ((TenderApproved)result).ApprovedAmount.Should().Be(100m);

        var payments = await _payments.GetByBillIdAsync(billId);
        payments.Should().ContainSingle();
        payments[0].Status.Should().Be(PaymentStatus.Approved);
        payments[0].TenderedAmount.Should().Be(100m);
        payments[0].ApprovedAmount.Should().Be(100m);
        payments[0].ChangeAmount.Should().Be(0m);

        var allocations = await _allocations.GetByBillIdAsync(billId);
        allocations.Should().ContainSingle(a => a.Amount == 100m);
    }

    [Fact]
    public async Task HandleAsyncAcceptsAPartialAmountLessThanTheRemainingPayable()
    {
        var billId = await SeedBillAsync(payable: 100m);
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 60m,
            BillId: billId, IdempotencyKey: Guid.NewGuid().ToString());

        var result = await _handler.HandleAsync(request);

        ((TenderApproved)result).ApprovedAmount.Should().Be(60m);
        var payments = await _payments.GetByBillIdAsync(billId);
        payments[0].ChangeAmount.Should().Be(0m, "EFT never gives change, even for an under-full tender");
    }

    [Fact]
    public async Task HandleAsyncRejectsAnAmountExceedingTheRemainingPayableAndCreatesNothing()
    {
        var billId = await SeedBillAsync(payable: 80m);
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 200m,
            BillId: billId, IdempotencyKey: Guid.NewGuid().ToString());

        var act = () => _handler.HandleAsync(request);

        var exception = await act.Should().ThrowAsync<EftOverTenderException>();
        exception.Which.Amount.Should().Be(200m);
        exception.Which.RemainingPayable.Should().Be(80m);
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _payments.GetByBillIdAsync(billId)).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncReplaysADuplicateSubmitInsteadOfCreatingASecondSetOfRecords()
    {
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 75m,
            BillId: billId, IdempotencyKey: idempotencyKey);

        var first = await _handler.HandleAsync(request);
        var second = await _handler.HandleAsync(request);

        ((TenderApproved)first).ApprovedAmount.Should().Be(((TenderApproved)second).ApprovedAmount);
        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await _payments.GetByBillIdAsync(billId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ConcurrentSubmitsOfTheSameCommandProduceExactlyOneRealSetOfRecords()
    {
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 40m,
            BillId: billId, IdempotencyKey: idempotencyKey);

        var first = _handler.HandleAsync(request);
        var second = _handler.HandleAsync(request);
        await Task.WhenAll(first, second);

        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await _payments.GetByBillIdAsync(billId)).Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Referans: 12345")]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleAsyncAcceptsAnOptionalFreeTextNoteInAnyForm(string? note)
    {
        var billId = await SeedBillAsync(payable: 50m);
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 50m,
            BillId: billId, IdempotencyKey: Guid.NewGuid().ToString(), Note: note);

        var act = () => _handler.HandleAsync(request);

        await act.Should().NotThrowAsync("a reference number/note is never required (Semih's own product decision)");
    }

    [Fact]
    public async Task HandleAsyncRejectsAMissingBillId()
    {
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 10m, IdempotencyKey: "x");

        var act = () => _handler.HandleAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsyncThrowsForABillThatDoesNotExist()
    {
        var request = new TenderRequest(
            Guid.NewGuid(), TenderMethod.Eft, Amount: 10m,
            BillId: Guid.NewGuid(), IdempotencyKey: Guid.NewGuid().ToString());

        var act = () => _handler.HandleAsync(request);

        await act.Should().ThrowAsync<EftTenderBillNotFoundException>();
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
