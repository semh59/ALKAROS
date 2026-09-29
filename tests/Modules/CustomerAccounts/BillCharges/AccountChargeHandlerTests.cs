using System.Security.Cryptography;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.CustomerAccounts.BillCharges.Tests.Fixtures;
using ALKAROS.CustomerAccounts.CreditTerms;
using ALKAROS.CustomerAccounts.TransactionLedger;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.ModuleComposition;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Secrets;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.CustomerAccounts.BillCharges.Tests;

/// <summary>
/// Integration tests for <see cref="AccountChargeHandler"/> against real
/// Postgres (V14-ACC-003). Mirrors
/// ALKAROS.Cash.TenderHandler.Tests.CashTenderHandlerTests' own shape and
/// seeding technique exactly.
/// </summary>
public sealed class AccountChargeHandlerTests : IClassFixture<AccountChargeTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresCustomerProfileStore _profiles;
    private readonly PostgresBillRepository _bills;
    private readonly PostgresBillAdjustmentRepository _adjustments;
    private readonly PostgresOrderRepository _orders;
    private readonly PostgresPaymentRepository _payments;
    private readonly PostgresPaymentAllocationRepository _allocations;
    private readonly PostgresAccountTransactionLedger _ledger;
    private readonly PostgresCustomerCreditTermsStore _creditTerms;

    public AccountChargeHandlerTests(AccountChargeTestDatabase database)
    {
        _dataSource = database.DataSource;
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _profiles = new PostgresCustomerProfileStore(_dataSource, secretProvider);
        _bills = new PostgresBillRepository(_dataSource);
        _adjustments = new PostgresBillAdjustmentRepository(_dataSource);
        _orders = new PostgresOrderRepository(_dataSource);
        _payments = new PostgresPaymentRepository(_dataSource);
        _allocations = new PostgresPaymentAllocationRepository(_dataSource, _adjustments);
        _ledger = new PostgresAccountTransactionLedger(_dataSource);
        _creditTerms = new PostgresCustomerCreditTermsStore(_dataSource);
    }

    private AccountChargeHandler Handler(ICustomerCreditPolicy? creditPolicy = null) =>
        new(_profiles, creditPolicy ?? new ApprovingCreditPolicy(), _bills, _adjustments, _payments, _allocations, _ledger, _dataSource);

    /// <summary>
    /// The handler's own mechanics are tested with a policy that approves;
    /// the production policy (V1-RMD-440) is tested below with real credit terms.
    /// </summary>
    private sealed class ApprovingCreditPolicy : ICustomerCreditPolicy
    {
        public Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken) =>
            Task.FromResult(new CreditPolicyResult(true, null));
    }

    [Fact]
    public async Task HandleAsyncCreatesAPaymentAnAllocationAndAnAccountChargeAtomically()
    {
        var customerId = await SeedCustomerAsync();
        var billId = await SeedBillAsync(payable: 150m);
        var request = new AccountChargeRequest(customerId, billId, AmountDue: 150m, IdempotencyKey: Guid.NewGuid().ToString());

        var result = await Handler().HandleAsync(request);

        result.ApprovedAmount.Should().Be(150m);
        result.WasReplayed.Should().BeFalse();

        var payment = await _payments.GetByIdAsync(result.PaymentId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Approved);
        payment.ApprovedAmount.Should().Be(150m);

        var allocation = (await _allocations.GetByBillIdAsync(billId)).Should().ContainSingle().Subject;
        allocation.Amount.Should().Be(150m);
        allocation.PaymentId.Should().Be(result.PaymentId);

        var accountTransaction = await _ledger.GetAsync(result.AccountTransactionId);
        accountTransaction.Should().NotBeNull();
        accountTransaction!.TransactionType.Should().Be(AccountTransactionType.Charge);
        accountTransaction.Direction.Should().Be(AccountTransactionDirection.Debit);
        accountTransaction.Amount.Should().Be(150m);
        accountTransaction.CustomerId.Should().Be(customerId);
        accountTransaction.SourceReferenceType.Should().Be("Payment");
        accountTransaction.SourceReferenceId.Should().Be(result.PaymentId);
    }

    [Fact]
    public async Task HandleAsyncRetryingTheSameIdempotencyKeyReplaysTheSameThreeRecords()
    {
        var customerId = await SeedCustomerAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var request = new AccountChargeRequest(customerId, billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        var first = await Handler().HandleAsync(request);
        var retry = await Handler().HandleAsync(request);

        retry.PaymentId.Should().Be(first.PaymentId);
        retry.PaymentAllocationId.Should().Be(first.PaymentAllocationId);
        retry.AccountTransactionId.Should().Be(first.AccountTransactionId);
        retry.WasReplayed.Should().BeTrue();

        (await _allocations.GetByBillIdAsync(billId)).Should().ContainSingle();
    }

    [Fact]
    public async Task AnAnonymizedCustomerCannotHaveAChargePostedAndNeitherBillNorAccountChanges()
    {
        var customerId = await SeedCustomerAsync();
        await _profiles.AnonymizeAsync(customerId, expectedRowVersion: 1);
        var billId = await SeedBillAsync(payable: 100m);
        var request = new AccountChargeRequest(customerId, billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<AccountChargeCustomerAnonymizedException>(() => Handler().HandleAsync(request));

        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetByCustomerAsync(customerId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnknownCustomerThrowsNotFoundBeforeTouchingAnything()
    {
        var billId = await SeedBillAsync(payable: 100m);
        var request = new AccountChargeRequest(Guid.NewGuid(), billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<CustomerProfileNotFoundException>(() => Handler().HandleAsync(request));

        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
    }

    private CreditTermsCreditPolicy ProductionPolicy() =>
        new(_creditTerms, new PostgresAccountBalanceProjection(_dataSource, _ledger), _dataSource);

    [Fact]
    public async Task TheProductionCreditPolicyRefusesAChargeWhenNoCreditLimitIsSet()
    {
        var customerId = await SeedCustomerAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var request = new AccountChargeRequest(customerId, billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        var denied = await Assert.ThrowsAsync<AccountChargeCreditPolicyDeniedException>(
            () => Handler(ProductionPolicy()).HandleAsync(request));

        denied.Reason.Should().Be("Müşteri için kredi limiti tanımlanmadığından cari hesaba borç yazılamaz.");
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetByCustomerAsync(customerId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AChargeWithinTheCustomersCreditLimitIsPosted()
    {
        var customerId = await SeedCustomerAsync();
        await _creditTerms.SetAsync(customerId, creditLimit: 500m, paymentTermDays: null, updatedBy: Guid.NewGuid());
        var billId = await SeedBillAsync(payable: 200m);

        var result = await Handler(ProductionPolicy()).HandleAsync(
            new AccountChargeRequest(customerId, billId, AmountDue: 200m, IdempotencyKey: Guid.NewGuid().ToString()));

        result.ApprovedAmount.Should().Be(200m);
        (await _ledger.GetByCustomerAsync(customerId)).Should().ContainSingle();
    }

    [Fact]
    public async Task AChargeThatWouldExceedTheCreditLimitIsRefusedAndPostsNothing()
    {
        var customerId = await SeedCustomerAsync();
        await _creditTerms.SetAsync(customerId, creditLimit: 500m, paymentTermDays: null, updatedBy: Guid.NewGuid());
        await RecordAsync(customerId, AccountTransactionType.Charge, 450m, DateTimeOffset.UtcNow);
        var billId = await SeedBillAsync(payable: 100m);

        var denied = await Assert.ThrowsAsync<AccountChargeCreditPolicyDeniedException>(
            () => Handler(ProductionPolicy()).HandleAsync(
                new AccountChargeRequest(customerId, billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString())));

        denied.Reason.Should().Be("Cari borç kredi limitini aşıyor: bakiye 450,00 TL, yeni borç 100,00 TL, limit 500,00 TL.");
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetByCustomerAsync(customerId)).Should().ContainSingle();
    }

    [Fact]
    public async Task OverdueDebtRefusesANewChargeUntilItIsPaid()
    {
        var customerId = await SeedCustomerAsync();
        await _creditTerms.SetAsync(customerId, creditLimit: 1000m, paymentTermDays: 30, updatedBy: Guid.NewGuid());
        await RecordAsync(customerId, AccountTransactionType.Charge, 120m, DateTimeOffset.UtcNow.AddDays(-40));
        await RecordAsync(customerId, AccountTransactionType.Payment, 20m, DateTimeOffset.UtcNow.AddDays(-5));
        var billId = await SeedBillAsync(payable: 50m);

        var denied = await Assert.ThrowsAsync<AccountChargeCreditPolicyDeniedException>(
            () => Handler(ProductionPolicy()).HandleAsync(
                new AccountChargeRequest(customerId, billId, AmountDue: 50m, IdempotencyKey: Guid.NewGuid().ToString())));
        denied.Reason.Should().Be("Müşterinin 30 günlük vadesi geçmiş 100,00 TL cari borcu var; ödenmeden yeni borç yazılamaz.");
        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();

        await RecordAsync(customerId, AccountTransactionType.Payment, 100m, DateTimeOffset.UtcNow);
        var result = await Handler(ProductionPolicy()).HandleAsync(
            new AccountChargeRequest(customerId, billId, AmountDue: 50m, IdempotencyKey: Guid.NewGuid().ToString()));
        result.ApprovedAmount.Should().Be(50m);
    }

    [Fact]
    public async Task ARecentChargeInsideThePaymentTermIsNotOverdue()
    {
        var customerId = await SeedCustomerAsync();
        await _creditTerms.SetAsync(customerId, creditLimit: 1000m, paymentTermDays: 30, updatedBy: Guid.NewGuid());
        await RecordAsync(customerId, AccountTransactionType.Charge, 120m, DateTimeOffset.UtcNow.AddDays(-10));
        var billId = await SeedBillAsync(payable: 50m);

        var result = await Handler(ProductionPolicy()).HandleAsync(
            new AccountChargeRequest(customerId, billId, AmountDue: 50m, IdempotencyKey: Guid.NewGuid().ToString()));

        result.ApprovedAmount.Should().Be(50m);
    }

    [Fact]
    public async Task TwoConcurrentChargesCannotTogetherExceedTheCreditLimit()
    {
        var customerId = await SeedCustomerAsync();
        await _creditTerms.SetAsync(customerId, creditLimit: 150m, paymentTermDays: null, updatedBy: Guid.NewGuid());
        var firstBill = await SeedBillAsync(payable: 100m);
        var secondBill = await SeedBillAsync(payable: 100m);

        var attempts = await Task.WhenAll(
            TryChargeAsync(customerId, firstBill, 100m),
            TryChargeAsync(customerId, secondBill, 100m));

        attempts.Count(approved => approved).Should().Be(1);
        (await _ledger.GetByCustomerAsync(customerId)).Should().ContainSingle();
    }

    private async Task<bool> TryChargeAsync(Guid customerId, Guid billId, decimal amount)
    {
        try
        {
            await Handler(ProductionPolicy()).HandleAsync(
                new AccountChargeRequest(customerId, billId, AmountDue: amount, IdempotencyKey: Guid.NewGuid().ToString()));
            return true;
        }
        catch (AccountChargeCreditPolicyDeniedException)
        {
            return false;
        }
    }

    [Fact]
    public async Task CreditTermsCannotBeSetForAnUnknownOrAnonymizedCustomer()
    {
        var anonymized = await SeedCustomerAsync();
        await _profiles.AnonymizeAsync(anonymized, expectedRowVersion: 1);

        (await _creditTerms.SetAsync(Guid.NewGuid(), 100m, null, Guid.NewGuid())).Should().BeNull();
        (await _creditTerms.SetAsync(anonymized, 100m, null, Guid.NewGuid())).Should().BeNull();
        (await _creditTerms.GetAsync(anonymized)).Should().BeNull();
    }

    [Fact]
    public void TheProductionModuleRegistersTheCreditTermsPolicy()
    {
        var context = new ModuleContext();
        new CustomerAccountsBillChargesModule().Register(context);

        context.Services
            .Where(service => service.ServiceType == typeof(ICustomerCreditPolicy))
            .Should().ContainSingle()
            .Which.ImplementationType.Should().Be<CreditTermsCreditPolicy>();
    }

    private async Task RecordAsync(Guid customerId, AccountTransactionType type, decimal amount, DateTimeOffset occurredAt) =>
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId, type, amount, sourceReferenceType: "Test", sourceReferenceId: Guid.NewGuid(),
            note: null, createdBy: null, occurredAt: occurredAt));

    private sealed class DenyingCreditPolicy(string reason) : ICustomerCreditPolicy
    {
        public Task<CreditPolicyResult> EvaluateAsync(Guid customerId, decimal amount, CancellationToken cancellationToken) =>
            Task.FromResult(new CreditPolicyResult(false, reason));
    }

    [Fact]
    public async Task ACreditPolicyDenialChangesNeitherTheBillNorTheAccount()
    {
        var customerId = await SeedCustomerAsync();
        var billId = await SeedBillAsync(payable: 100m);
        var request = new AccountChargeRequest(customerId, billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<AccountChargeCreditPolicyDeniedException>(
            () => Handler(new DenyingCreditPolicy("over limit")).HandleAsync(request));

        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetByCustomerAsync(customerId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnknownBillThrowsNotFoundAfterEligibilityButBeforeAnyWrite()
    {
        var customerId = await SeedCustomerAsync();
        var request = new AccountChargeRequest(customerId, Guid.NewGuid(), AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<AccountChargeBillNotFoundException>(() => Handler().HandleAsync(request));

        (await _ledger.GetByCustomerAsync(customerId)).Should().BeEmpty();
    }

    [Fact]
    public async Task OverAllocatingABillIsRejectedAndPostsNothing()
    {
        var customerId = await SeedCustomerAsync();
        var billId = await SeedBillAsync(payable: 50m);
        var request = new AccountChargeRequest(customerId, billId, AmountDue: 100m, IdempotencyKey: Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<OverAllocationException>(() => Handler().HandleAsync(request));

        (await _allocations.GetByBillIdAsync(billId)).Should().BeEmpty();
        (await _ledger.GetByCustomerAsync(customerId)).Should().BeEmpty();
    }

    private async Task<Guid> SeedCustomerAsync() =>
        await _profiles.CreateAsync(new CreateCustomerProfileRequest("Test Customer", null, null, null));

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
