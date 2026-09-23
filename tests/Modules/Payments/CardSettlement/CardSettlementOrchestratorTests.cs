using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.CardSettlement.Tests.Fixtures;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Payments.TenderRouting;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.CardSettlement.Tests;

/// <summary>
/// Integration tests for <see cref="CardSettlementOrchestrator"/> against
/// real Postgres (V13-PAY-004). Exercises the three
/// <see cref="TenderHandlerResult"/> branches, crash-and-resume replay,
/// provider-correlation mismatch rejection, and a genuinely concurrent
/// duplicate submit.
/// </summary>
public sealed class CardSettlementOrchestratorTests : IClassFixture<CardSettlementTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresCardSettlementAttemptRepository _attempts;
    private readonly CardSettlementOrchestrator _orchestrator;

    public CardSettlementOrchestratorTests(CardSettlementTestDatabase database)
    {
        _dataSource = database.DataSource;
        _bills = new PostgresBillRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _payments = new PostgresPaymentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource);
        _attempts = new PostgresCardSettlementAttemptRepository();
        _orchestrator = new CardSettlementOrchestrator(_bills, _payments, _allocations, _attempts, _dataSource);
    }

    [Fact]
    public async Task ApprovedSettlementCreatesPaymentAllocationAndQueuesFiscalHandoff()
    {
        var billId = await SeedBillAsync(payable: 80m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, ProviderCorrelationId: "TXN-1",
            new TenderApproved(80m));

        var result = await _orchestrator.HandleAsync(request);

        result.Outcome.Should().Be(CardSettlementOutcome.Approved);
        result.ApprovedAmount.Should().Be(80m);
        result.AllocationId.Should().NotBeNull();
        result.WasReplayed.Should().BeFalse();

        var payment = await _payments.GetByIdAsync(result.PaymentId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Approved);
        payment.ApprovedAmount.Should().Be(80m);
        payment.ChangeAmount.Should().Be(0m);

        var allocations = await _allocations.GetByBillIdAsync(billId);
        allocations.Should().ContainSingle(a => a.Id == result.AllocationId && a.Amount == 80m);

        (await CountOutboxMessagesForPaymentAsync(result.PaymentId, "card-settlement.approved")).Should().Be(1);
    }

    [Fact]
    public async Task DeclinedSettlementNeverAllocatesOrQueuesFiscalHandoff()
    {
        var billId = await SeedBillAsync(payable: 50m);
        var request = new CardSettlementRequest(
            billId, AttemptedAmount: 50m, Guid.NewGuid().ToString(), "TXN-2",
            new TenderDeclined("insufficient_funds"));

        var result = await _orchestrator.HandleAsync(request);

        result.Outcome.Should().Be(CardSettlementOutcome.Declined);
        result.AllocationId.Should().BeNull();
        result.ApprovedAmount.Should().BeNull();

        var payment = await _payments.GetByIdAsync(result.PaymentId);
        payment!.Status.Should().Be(PaymentStatus.Declined);

        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await CountOutboxMessagesForPaymentAsync(result.PaymentId, "card-settlement.approved")).Should().Be(0);
    }

    [Fact]
    public async Task RequiresReconciliationSettlementLeavesPaymentUnknownAsTypedEvidenceForV13Rec001()
    {
        var billId = await SeedBillAsync(payable: 60m);
        var request = new CardSettlementRequest(
            billId, AttemptedAmount: 60m, Guid.NewGuid().ToString(), "TXN-3",
            new TenderRequiresReconciliation("provider_timeout"));

        var result = await _orchestrator.HandleAsync(request);

        result.Outcome.Should().Be(CardSettlementOutcome.RequiresReconciliation);
        result.AllocationId.Should().BeNull();
        result.ApprovedAmount.Should().BeNull();

        var payment = await _payments.GetByIdAsync(result.PaymentId);
        payment!.Status.Should().Be(PaymentStatus.Unknown);

        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await CountOutboxMessagesForPaymentAsync(result.PaymentId, "card-settlement.approved")).Should().Be(0);
    }

    [Fact]
    public async Task ResumeAfterACrashReplaysTheSameAllocationInsteadOfCreatingASecondOne()
    {
        var billId = await SeedBillAsync(payable: 80m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, "TXN-RESUME",
            new TenderApproved(80m));

        var first = await _orchestrator.HandleAsync(request);
        // Simulates the caller retrying after a crash occurred after commit
        // but before the first call's response ever reached it.
        var second = await _orchestrator.HandleAsync(request);

        second.WasReplayed.Should().BeTrue();
        second.PaymentId.Should().Be(first.PaymentId);
        second.AllocationId.Should().Be(first.AllocationId);
        second.ApprovedAmount.Should().Be(first.ApprovedAmount);

        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await _payments.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await CountOutboxMessagesForPaymentAsync(first.PaymentId, "card-settlement.approved")).Should().Be(1);
    }

    [Fact]
    public async Task ADifferentProviderCorrelationUnderTheSameIdempotencyKeyIsRejectedAsAMismatch()
    {
        var billId = await SeedBillAsync(payable: 80m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var original = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, "TXN-ORIGINAL", new TenderApproved(80m));
        var first = await _orchestrator.HandleAsync(original);

        var mismatched = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, "TXN-DIFFERENT", new TenderApproved(80m));

        var act = () => _orchestrator.HandleAsync(mismatched);

        await act.Should().ThrowAsync<CardSettlementReplayMismatchException>();
        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await CountOutboxMessagesForPaymentAsync(first.PaymentId, "card-settlement.approved")).Should().Be(1);
    }

    [Fact]
    public async Task ADifferentOutcomeUnderTheSameIdempotencyKeyIsRejectedAsAMismatch()
    {
        var billId = await SeedBillAsync(payable: 80m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var approved = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, "TXN-SAME", new TenderApproved(80m));
        await _orchestrator.HandleAsync(approved);

        var declinedReplay = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, "TXN-SAME", new TenderDeclined("changed_mind"));

        var act = () => _orchestrator.HandleAsync(declinedReplay);

        await act.Should().ThrowAsync<CardSettlementReplayMismatchException>();
        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ConcurrentSubmitsOfTheSameAttemptProduceExactlyOneAllocation()
    {
        var billId = await SeedBillAsync(payable: 80m);
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CardSettlementRequest(
            billId, AttemptedAmount: 80m, idempotencyKey, "TXN-CONCURRENT", new TenderApproved(80m));

        var first = _orchestrator.HandleAsync(request);
        var second = _orchestrator.HandleAsync(request);
        var results = await Task.WhenAll(first, second);

        results[0].AllocationId.Should().Be(results[1].AllocationId);
        results.Should().ContainSingle(r => !r.WasReplayed);
        results.Should().ContainSingle(r => r.WasReplayed);

        (await _allocations.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await _payments.GetByBillIdAsync(billId)).Should().HaveCount(1);
        (await CountOutboxMessagesForPaymentAsync(results[0].PaymentId, "card-settlement.approved")).Should().Be(1);
    }

    [Fact]
    public async Task UnknownBillIsRejectedAndCreatesNothing()
    {
        var request = new CardSettlementRequest(
            Guid.NewGuid(), AttemptedAmount: 10m, Guid.NewGuid().ToString(), "TXN-NOBILL",
            new TenderApproved(10m));

        var act = () => _orchestrator.HandleAsync(request);

        await act.Should().ThrowAsync<CardSettlementBillNotFoundException>();
    }

    /// <summary>
    /// Counts outbox rows for one payment's own settlement attempt(s) only —
    /// the test database is shared across the whole class fixture, so an
    /// unscoped count would accumulate across unrelated test methods.
    /// </summary>
    private async Task<long> CountOutboxMessagesForPaymentAsync(Guid paymentId, string eventType)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT count(*) FROM outbox_messages om
            JOIN payments.card_settlement_attempts a ON a.card_settlement_attempt_id = om.aggregate_id
            WHERE a.payment_id = @payment_id AND om.event_type = @event_type;
            """);
        command.Parameters.AddWithValue("payment_id", paymentId);
        command.Parameters.AddWithValue("event_type", eventType);
        return (long)(await command.ExecuteScalarAsync())!;
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
