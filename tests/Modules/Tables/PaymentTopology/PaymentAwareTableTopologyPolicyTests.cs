using ALKAROS.Tables.PaymentTopology.Tests.Fixtures;
using ALKAROS.Tables.TableMerge;
using ALKAROS.Tables.TableTransfer;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Tables.PaymentTopology.Tests;

/// <summary>
/// Unit-level tests for <see cref="PaymentAwareTableTopologyPolicy"/>
/// (V13-TBL-001). The end-to-end proof that real transfer/merge/unmerge
/// calls honor this policy atomically lives in
/// <c>ALKAROS.Tables.TableTransfer.Tests</c>/<c>ALKAROS.Tables.TableMerge.Tests</c>
/// — these tests isolate the policy's own decision logic.
/// </summary>
public sealed class PaymentAwareTableTopologyPolicyTests : IClassFixture<PaymentTopologyTestDatabase>, IAsyncLifetime
{
    private readonly PaymentTopologyTestDatabase _db;

    public PaymentAwareTableTopologyPolicyTests(PaymentTopologyTestDatabase db) => _db = db;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NoBillsGivenIsANoOp()
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var act = () => PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentAsync(
            [], connection, transaction);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BillWithNoPaymentIsAllowed()
    {
        var billId = await CreateBillAsync();

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var act = () => PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentAsync(
            [billId], connection, transaction);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    public async Task BillWithSettledPaymentIsAllowed(string settledStatus)
    {
        var billId = await CreateBillAsync();
        await CreatePaymentAsync(billId, settledStatus);

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var act = () => PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentAsync(
            [billId], connection, transaction);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Unknown")]
    [InlineData("ReconciliationRequired")]
    public async Task BillWithUnsettledPaymentThrowsWithTheBillId(string unsettledStatus)
    {
        var billId = await CreateBillAsync();
        await CreatePaymentAsync(billId, unsettledStatus);

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var act = () => PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentAsync(
            [billId], connection, transaction);

        var ex = await act.Should().ThrowAsync<TableTransfer.PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);
    }

    [Fact]
    public async Task MergeVariantThrowsTheMergeExceptionType()
    {
        var billId = await CreateBillAsync();
        await CreatePaymentAsync(billId, "Unknown");

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var act = () => PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentForMergeAsync(
            [billId], connection, transaction);

        var ex = await act.Should().ThrowAsync<TableMerge.PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(billId);
    }

    [Fact]
    public async Task OneOfSeveralBillsUnsettledThrowsForThatBillOnly()
    {
        var settledBillId = await CreateBillAsync();
        await CreatePaymentAsync(settledBillId, "Approved");

        var unsettledBillId = await CreateBillAsync();
        await CreatePaymentAsync(unsettledBillId, "Pending");

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var act = () => PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentAsync(
            [settledBillId, unsettledBillId], connection, transaction);

        var ex = await act.Should().ThrowAsync<TableTransfer.PaymentPolicyRequiredException>();
        ex.Which.BillId.Should().Be(unsettledBillId);
    }

    private async Task<Guid> CreateBillAsync()
    {
        var id = Guid.NewGuid();
        await _db.ExecuteAsync(
            """
            INSERT INTO billing.bills (
                bill_id, bill_number, status, subtotal, discount_total, tax_total,
                payable_amount, allocated_amount, paid_amount, change_amount,
                currency_code, opened_at, created_at, updated_at, row_version
            ) VALUES (
                @id, @bill_number, 'Open', 0, 0, 0,
                100, 0, 0, 0,
                'TRY', now(), now(), now(), 1
            );
            """,
            ("id", id),
            ("bill_number", $"BIL-{id:N}"[..20]));
        return id;
    }

    private async Task CreatePaymentAsync(Guid billId, string status)
    {
        await _db.ExecuteAsync(
            """
            INSERT INTO payments.payments (
                payment_id, bill_id, status, currency_code, requested_amount,
                tendered_amount, approved_amount, change_amount,
                initiated_at, tendered_at, created_at, updated_at, row_version
            ) VALUES (
                @id, @bill_id, @status, 'TRY', 100,
                100, NULL, 0,
                now(), now(), now(), now(), 1
            );
            """,
            ("id", Guid.NewGuid()),
            ("bill_id", billId),
            ("status", status));
    }
}
