using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence.Tests.Fixtures;
using ALKAROS.Payments.PaymentAggregate;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Payments.Allocations.Persistence.Tests;

/// <summary>
/// Pure (no database) tests for <see cref="PaymentAllocationFactory"/> and
/// <see cref="PaymentAllocation"/>'s own constructor invariants.
/// </summary>
public sealed class PaymentAllocationFactoryTests
{
    [Fact]
    public void CreateRejectsAZeroOrNegativeAmount()
    {
        var (payment, bill) = MakeMatchingPair();

        var actZero = () => PaymentAllocationFactory.Create(payment, bill, 0m, alreadyAllocatedForBill: 0m, "key-1");
        actZero.Should().Throw<InvalidPaymentAllocationAmountException>();

        var actNegative = () => PaymentAllocationFactory.Create(payment, bill, -10m, alreadyAllocatedForBill: 0m, "key-2");
        actNegative.Should().Throw<InvalidPaymentAllocationAmountException>();
    }

    [Fact]
    public void CreateRejectsAnAllocationTargetingADifferentBillThanThePayment()
    {
        var (payment, _) = MakeMatchingPair();
        var otherBill = new Bill(Guid.NewGuid(), "BILL-OTHER-01", currencyCode: "TRY");

        var act = () => PaymentAllocationFactory.Create(payment, otherBill, 10m, alreadyAllocatedForBill: 0m, "key-3");

        act.Should().Throw<CrossBillPaymentAllocationException>()
            .Where(ex => ex.PaymentBillId == payment.BillId && ex.TargetBillId == otherBill.Id);
    }

    [Fact]
    public void CreateRejectsACurrencyMismatchBetweenPaymentAndBill()
    {
        var billId = Guid.NewGuid();
        var bill = new Bill(billId, "BILL-EUR-01", currencyCode: "EUR");
        var payment = new Payment(Guid.NewGuid(), billId, 80m, currencyCode: "TRY");

        var act = () => PaymentAllocationFactory.Create(payment, bill, 10m, alreadyAllocatedForBill: 0m, "key-4");

        act.Should().Throw<CurrencyMismatchAllocationException>()
            .Where(ex => ex.PaymentCurrency == "TRY" && ex.BillCurrency == "EUR");
    }

    [Fact]
    public void CreateRejectsAnAmountExceedingTheBillsRemainingPayable()
    {
        var (payment, bill) = MakeMatchingPair(payableAmount: 80m);

        // 50 already allocated, remaining is 30 - requesting 31 must reject.
        var act = () => PaymentAllocationFactory.Create(payment, bill, 31m, alreadyAllocatedForBill: 50m, "key-5");

        act.Should().Throw<OverAllocationException>()
            .Where(ex => ex.BillId == bill.Id && ex.RemainingPayable == 30m);
    }

    [Fact]
    public void CreateAllowsAnAmountExactlyMatchingTheRemainingPayable()
    {
        var (payment, bill) = MakeMatchingPair(payableAmount: 80m);

        var allocation = PaymentAllocationFactory.Create(payment, bill, 30m, alreadyAllocatedForBill: 50m, "key-6");

        allocation.Amount.Should().Be(30m);
        allocation.CurrencyCode.Should().Be("TRY");
        allocation.PaymentId.Should().Be(payment.Id);
        allocation.BillId.Should().Be(bill.Id);
    }

    private static (Payment Payment, Bill Bill) MakeMatchingPair(decimal payableAmount = 80m)
    {
        var billId = Guid.NewGuid();
        var billItem = new BillItem(
            Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Test Item",
            quantity: 1, unitPrice: payableAmount, taxRate: 0);
        var bill = new Bill(billId, "BILL-MATCH-01", items: [billItem], currencyCode: "TRY");
        var payment = new Payment(Guid.NewGuid(), billId, payableAmount, currencyCode: "TRY");
        return (payment, bill);
    }
}

/// <summary>
/// Integration tests for PostgresPaymentAllocationRepository against real
/// Postgres. Exercises the app-level factory invariants end to end through
/// the real repository, the database-level composite FK/UNIQUE/CHECK
/// constraints backing them up (defense in depth, matching
/// PostgresPaymentTests' own pattern), idempotent replay, and the
/// concurrent-allocation race guarded by the per-bill advisory lock.
/// </summary>
public sealed class PostgresPaymentAllocationRepositoryTests : IClassFixture<PaymentAllocationTestDatabase>
{
    private const string ForeignKeyViolation = "23503";
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";

    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresOrderRepository _orders;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPaymentAllocationRepositoryTests(PaymentAllocationTestDatabase database)
    {
        _dataSource = database.DataSource;
        _allocations = new PostgresPaymentAllocationRepository(database.DataSource);
        _payments = new PostgresPaymentRepository(database.DataSource);
        _bills = new PostgresBillRepository(database.DataSource);
        _orders = new PostgresOrderRepository(database.DataSource);
    }

    [Fact]
    public async Task AllocateAsyncPersistsAndRoundTrips()
    {
        var (payment, bill) = await SeedPaymentAndBillAsync(payable: 80m);

        var allocation = await _allocations.AllocateAsync(payment, bill, 80m, "idem-round-trip-1");

        allocation.Amount.Should().Be(80m);
        var loaded = await _allocations.GetByIdempotencyKeyAsync("idem-round-trip-1");
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(allocation.Id);
        var byBill = await _allocations.GetByBillIdAsync(bill.Id);
        byBill.Should().ContainSingle(a => a.Id == allocation.Id);
    }

    [Fact]
    public async Task AllocateAsyncReplayingTheSameIdempotencyKeyReturnsTheExistingRowWithoutInsertingASecondOne()
    {
        var (payment, bill) = await SeedPaymentAndBillAsync(payable: 80m);

        var first = await _allocations.AllocateAsync(payment, bill, 50m, "idem-replay-1");
        var replay = await _allocations.AllocateAsync(payment, bill, 50m, "idem-replay-1");

        replay.Id.Should().Be(first.Id);
        var byBill = await _allocations.GetByBillIdAsync(bill.Id);
        byBill.Should().HaveCount(1);
    }

    [Fact]
    public async Task AllocateAsyncRejectsAnOverAllocationThroughARealRepositoryRoundTrip()
    {
        var (payment, bill) = await SeedPaymentAndBillAsync(payable: 80m);
        await _allocations.AllocateAsync(payment, bill, 50m, "idem-over-1a");

        var act = () => _allocations.AllocateAsync(payment, bill, 31m, "idem-over-1b");

        await act.Should().ThrowAsync<OverAllocationException>()
            .Where(ex => ex.RemainingPayable == 30m);
    }

    [Fact]
    public async Task ConcurrentAllocationsAgainstTheSameBillNeverTogetherOverAllocateIt()
    {
        // V0-DOM-004 positive example 2's shape (bill payable 80, two
        // payments each individually within it but together over it) —
        // driven concurrently to prove the per-bill advisory lock actually
        // serializes them, not just that the sequential case works.
        var (paymentA, bill) = await SeedPaymentAndBillAsync(payable: 80m);
        var paymentB = new Payment(Guid.NewGuid(), bill.Id, 50m, currencyCode: "TRY");
        await _payments.AddAsync(paymentB);

        var resultsAndErrors = await Task.WhenAll(
            AllocateOrNullAsync(paymentA, bill, 50m, "idem-race-a"),
            AllocateOrNullAsync(paymentB, bill, 50m, "idem-race-b"));

        var succeeded = resultsAndErrors.Count(r => r);
        succeeded.Should().Be(1, "the second concurrent allocation must lose the advisory lock race and reject, not both succeed and over-allocate the bill");

        var total = (await _allocations.GetByBillIdAsync(bill.Id)).Sum(a => a.Amount);
        total.Should().Be(50m);
    }

    private async Task<bool> AllocateOrNullAsync(Payment payment, Bill bill, decimal amount, string idempotencyKey)
    {
        try
        {
            await _allocations.AllocateAsync(payment, bill, amount, idempotencyKey);
            return true;
        }
        catch (OverAllocationException)
        {
            return false;
        }
    }

    [Fact]
    public async Task DatabaseRejectsAnAllocationTargetingABillDifferentFromThePaymentsOwnBill()
    {
        var (payment, _) = await SeedPaymentAndBillAsync(payable: 80m);
        var (_, otherBill) = await SeedPaymentAndBillAsync(payable: 80m);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payment_allocations (
                payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES (@id, @payment_id, @bill_id, 10, 'TRY', @idem, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("payment_id", payment.Id);
        command.Parameters.AddWithValue("bill_id", otherBill.Id);
        command.Parameters.AddWithValue("idem", Guid.NewGuid().ToString());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsACurrencyThatDoesNotMatchThePaymentsOwnCurrency()
    {
        var (payment, bill) = await SeedPaymentAndBillAsync(payable: 80m);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payment_allocations (
                payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES (@id, @payment_id, @bill_id, 10, 'EUR', @idem, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("payment_id", payment.Id);
        command.Parameters.AddWithValue("bill_id", bill.Id);
        command.Parameters.AddWithValue("idem", Guid.NewGuid().ToString());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsADuplicateIdempotencyKey()
    {
        var (payment, bill) = await SeedPaymentAndBillAsync(payable: 80m);
        await _allocations.AllocateAsync(payment, bill, 10m, "idem-dup-1");

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payment_allocations (
                payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES (@id, @payment_id, @bill_id, 10, 'TRY', 'idem-dup-1', now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("payment_id", payment.Id);
        command.Parameters.AddWithValue("bill_id", bill.Id);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task DatabaseRejectsAZeroOrNegativeAmountEvenIfSomeFutureCallerBypassesTheFactory()
    {
        var (payment, bill) = await SeedPaymentAndBillAsync(payable: 80m);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO payments.payment_allocations (
                payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES (@id, @payment_id, @bill_id, 0, 'TRY', @idem, now());
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("payment_id", payment.Id);
        command.Parameters.AddWithValue("bill_id", bill.Id);
        command.Parameters.AddWithValue("idem", Guid.NewGuid().ToString());

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(CheckViolation, exception.SqlState);
    }

    private async Task<(Payment Payment, Bill Bill)> SeedPaymentAndBillAsync(decimal payable)
    {
        var billId = await SeedBillAsync(payable);
        var bill = await _bills.GetByIdAsync(billId);
        bill.Should().NotBeNull();
        var payment = new Payment(Guid.NewGuid(), billId, payable, currencyCode: "TRY");
        await _payments.AddAsync(payment);
        return (payment, bill!);
    }

    private async Task<Guid> SeedBillAsync(decimal price)
    {
        var productId = await SeedProductAsync("Iskender", price);
        var tableId = await SeedTableAsync();
        var order = await CreateAndSaveOrderAsync(productId, "Iskender", price, tableId);

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
            source: OrderSource.Waiter,
            orderNumber: "ORD-" + Guid.NewGuid().ToString("N")[..8],
            items: [item],
            tableId: tableId);

        await _orders.AddAsync(order);
        return order;
    }
}
