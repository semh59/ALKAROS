using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TenderHandler.Tests.Fixtures;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Cash.TenderHandler.Tests;

/// <summary>
/// Integration tests for <see cref="CashTenderHandler"/> against real
/// Postgres (V13-CSH-003). Exercises the open-session guard, insufficient-
/// tender rejection, atomic Payment+PaymentAllocation+CashTransaction
/// creation (all-or-nothing under a genuine failure), idempotent replay of
/// a duplicate submit, and a truly concurrent duplicate submit.
/// </summary>
public sealed class CashTenderHandlerTests : IClassFixture<CashTenderHandlerTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresCashTransactionLedgerRepository _ledger;
    private readonly PostgresCashSessionRepository _sessions;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresBillAdjustmentRepository _adjustments;
    private readonly CashSessionLifecycleService _sessionService;
    private readonly CashTenderHandler _handler;

    public CashTenderHandlerTests(CashTenderHandlerTestDatabase database)
    {
        _dataSource = database.DataSource;
        _payments = new PostgresPaymentRepository(_dataSource);
        _adjustments = new PostgresBillAdjustmentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource, _adjustments);
        _ledger = new PostgresCashTransactionLedgerRepository(_dataSource);
        _sessions = new PostgresCashSessionRepository(_dataSource);
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _sessionService = new CashSessionLifecycleService(_sessions, new CashSessionPolicy());
        _handler = new CashTenderHandler(_sessions, _bills, _payments, _allocations, _ledger, _adjustments, _dataSource);
    }

    /// <summary>
    /// V1-RMD-298 (independent 2026-09-26 audit, finding K1): a voluntary tip raises the real payable
    /// ceiling above the Bill's own ORIGINAL, never-updated PayableAmount (V0-DOM-004 - the tip only ever
    /// lands in billing.bill_adjustments). Before this fix, this cash tender for 100 against an original
    /// payable of 80 would have thrown OverAllocationException, making a tip un-collectible in cash the
    /// moment the running total would pass the bill's original amount.
    /// </summary>
    [Fact]
    public async Task HandleAsyncAcceptsATenderThatIncludesATipEvenThoughItExceedsTheOriginalPayable()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 80m);
        await _adjustments.AddAsync(BillAdjustment.CreateTip(
            Guid.NewGuid(), billId, amount: 20m, reason: "Test tip", authorizedBy: Guid.NewGuid()));
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 100m, TenderedAmount: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        var result = await _handler.HandleAsync(request);

        result.ApprovedAmount.Should().Be(100m);
        (await _allocations.GetByBillIdAsync(billId)).Should().ContainSingle(a => a.Amount == 100m);
    }

    [Fact]
    public async Task HandleAsyncCreatesExactlyOnePaymentAllocationAndCashTransactionWithDeterministicChange()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 80m, TenderedAmount: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        var result = await _handler.HandleAsync(request);

        result.ApprovedAmount.Should().Be(80m);
        result.ChangeAmount.Should().Be(20m);
        result.WasReplayed.Should().BeFalse();

        var payment = await _payments.GetByIdAsync(result.PaymentId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Approved);
        payment.TenderedAmount.Should().Be(100m);
        payment.ApprovedAmount.Should().Be(80m);
        payment.ChangeAmount.Should().Be(20m);

        var allocations = await _allocations.GetByBillIdAsync(billId);
        allocations.Should().ContainSingle(a => a.Id == result.PaymentAllocationId && a.Amount == 80m);

        var ledgerEntries = await _ledger.GetBySessionIdAsync(sessionId);
        ledgerEntries.Should().ContainSingle(e =>
            e.Id == result.CashTransactionId
            && e.Type == CashTransactionType.Sale
            && e.Amount == 80m
            && e.Direction == CashTransactionDirection.In
            && e.RelatedPaymentId == result.PaymentId);
    }

    [Fact]
    public async Task HandleAsyncRejectsATenderOnAClosedSessionAndCreatesNothing()
    {
        var sessionId = await SeedClosedSessionAsync();
        var billId = await SeedBillAsync(payable: 50m);
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 50m, TenderedAmount: 50m, IdempotencyKey: Guid.NewGuid().ToString());

        var act = () => _handler.HandleAsync(request);

        await act.Should().ThrowAsync<ClosedCashSessionException>();
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncRejectsATenderBelowTheAmountDueAndCreatesNothing()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 80m, TenderedAmount: 50m, IdempotencyKey: Guid.NewGuid().ToString());

        var act = () => _handler.HandleAsync(request);

        var exception = await act.Should().ThrowAsync<InsufficientCashTenderException>();
        exception.Which.TenderedAmount.Should().Be(50m);
        exception.Which.AmountDue.Should().Be(80m);
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncLeavesNoPartialPaymentOrCashTransactionWhenTheAllocationStepFails()
    {
        // The bill's own payable is 80 but the request claims a 200 amount
        // due (a caller bug/stale total) - Payment and the CashTransaction
        // would both insert cleanly before AllocateAsync's own
        // over-allocation check rejects it. Proves the whole transaction
        // rolls back together rather than leaving those two committed with
        // no matching allocation.
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 80m);
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 200m, TenderedAmount: 200m, IdempotencyKey: Guid.NewGuid().ToString());

        var act = () => _handler.HandleAsync(request);

        await act.Should().ThrowAsync<OverAllocationException>();
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().BeEmpty();
        var billPayments = await _payments.GetByBillIdAsync(billId);
        billPayments.Should().BeEmpty("the Payment insert must have rolled back with the rest of the transaction");
    }

    [Fact]
    public async Task HandleAsyncReplaysADuplicateSubmitInsteadOfCreatingASecondSetOfRecords()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 80m, TenderedAmount: 100m, IdempotencyKey: idempotencyKey);

        var first = await _handler.HandleAsync(request);
        var second = await _handler.HandleAsync(request);

        second.WasReplayed.Should().BeTrue();
        second.PaymentId.Should().Be(first.PaymentId);
        second.PaymentAllocationId.Should().Be(first.PaymentAllocationId);
        second.CashTransactionId.Should().Be(first.CashTransactionId);
        second.ApprovedAmount.Should().Be(first.ApprovedAmount);
        second.ChangeAmount.Should().Be(first.ChangeAmount);

        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().HaveCount(1);
        (await _payments.GetByBillIdAsync(billId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ACashTenderReusingAKeyAnotherTenderMethodUsedIsRefusedAndRecordsNothing()
    {
        // V1-RMD-415 (V1-RMD-393 F-12): an EFT tender already recorded under this key; no cash Sale stands behind
        // it, so a cash tender with the same key is neither new nor a replay.
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var eftPaymentId = Guid.NewGuid();
        await using (var eft = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, tendered_amount, approved_amount, initiated_at, tendered_at, approved_at, created_at, updated_at)
            VALUES (@payment, @bill, 'Approved', 40, 40, 40, now(), now(), now(), now(), now());
            INSERT INTO payments.payment_allocations (payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES (gen_random_uuid(), @payment, @bill, 40, 'TRY', @key, now());
            """))
        {
            eft.Parameters.AddWithValue("payment", eftPaymentId);
            eft.Parameters.AddWithValue("bill", billId);
            eft.Parameters.AddWithValue("key", idempotencyKey);
            await eft.ExecuteNonQueryAsync();
        }

        var act = () => _handler.HandleAsync(
            new CashTenderRequest(sessionId, billId, AmountDue: 40m, TenderedAmount: 40m, IdempotencyKey: idempotencyKey));

        await act.Should().ThrowAsync<CashTenderIdempotencyKeyReusedException>();
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().BeEmpty();
        (await _payments.GetByBillIdAsync(billId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ACashKeyReusedForAnotherAmountIsRefusedNotReplayed()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();
        await _handler.HandleAsync(
            new CashTenderRequest(sessionId, billId, AmountDue: 30m, TenderedAmount: 30m, IdempotencyKey: idempotencyKey));

        var act = () => _handler.HandleAsync(
            new CashTenderRequest(sessionId, billId, AmountDue: 50m, TenderedAmount: 50m, IdempotencyKey: idempotencyKey));

        await act.Should().ThrowAsync<CashTenderIdempotencyKeyReusedException>();
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ConcurrentSubmitsOfTheSameCommandProduceExactlyOneRealSetOfRecords()
    {
        var sessionId = await SeedOpenSessionAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CashTenderRequest(sessionId, billId, AmountDue: 80m, TenderedAmount: 100m, IdempotencyKey: idempotencyKey);

        var first = _handler.HandleAsync(request);
        var second = _handler.HandleAsync(request);
        var results = await Task.WhenAll(first, second);

        results[0].PaymentAllocationId.Should().Be(results[1].PaymentAllocationId);
        results.Should().ContainSingle(r => !r.WasReplayed);
        results.Should().ContainSingle(r => r.WasReplayed);

        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().HaveCount(1);
        (await _payments.GetByBillIdAsync(billId)).Should().HaveCount(1);
    }

    private async Task<Guid> SeedOpenSessionAsync()
    {
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);
        var (session, _) = await _sessionService.OpenSessionAsync(command);
        return session.CashSessionId;
    }

    private async Task<Guid> SeedClosedSessionAsync()
    {
        var sessionId = await SeedOpenSessionAsync();
        await _sessionService.CloseSessionAsync(
            new CloseCashSessionCommand(sessionId, ActualCash: 100m, ClosedBy: Guid.NewGuid()),
            expectedCash: 100m);
        return sessionId;
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
