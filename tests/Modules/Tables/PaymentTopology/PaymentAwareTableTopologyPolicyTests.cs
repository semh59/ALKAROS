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

    [Fact]
    public async Task EnsureNoUnsettledPaymentAsyncHoldsTheBillSettlementLockAcrossItsOwnTransaction()
    {
        // V1-RMD-258: proves the real fix for the Faz 2 independent audit's
        // TOCTOU finding — EnsureNoUnsettledPaymentAsync must hold the SAME
        // "bill-settlement:{billId:N}" advisory lock that
        // CardSettlementOrchestrator/EftTenderHandler take before writing a
        // Payment, so a concurrent settlement attempt against the same bill
        // genuinely cannot proceed while a transfer/merge/unmerge's own gate
        // check is still open. Uses pg_try_advisory_xact_lock (non-blocking)
        // from a SECOND real connection to observe this without hanging the
        // test — the second connection must fail to acquire the lock while
        // the first transaction (which called the real policy method) is
        // still open, and must succeed once it rolls back.
        var billId = await CreateBillAsync();

        await using var firstConnection = await _db.DataSource.OpenConnectionAsync();
        await using var firstTransaction = await firstConnection.BeginTransactionAsync();

        await PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentAsync(
            [billId], firstConnection, firstTransaction);

        await using var secondConnection = await _db.DataSource.OpenConnectionAsync();
        var stillHeld = await TryAcquireBillSettlementLockAsync(secondConnection, billId);
        stillHeld.Should().BeFalse(
            "EnsureNoUnsettledPaymentAsync should still be holding the bill-settlement lock inside its caller's open transaction");

        await firstTransaction.RollbackAsync();

        var releasedAfterRollback = await TryAcquireBillSettlementLockAsync(secondConnection, billId);
        releasedAfterRollback.Should().BeTrue(
            "the advisory lock must release once the transaction that held it ends");
    }

    /// <summary>
    /// Mirrors the exact key format <c>CardSettlementOrchestrator</c>/
    /// <c>EftTenderHandler</c>/<c>PaymentAwareTableTopologyPolicy</c> all use
    /// (<c>"bill-settlement:{billId:N}"</c>) via the non-blocking
    /// <c>pg_try_advisory_xact_lock</c>, so this test can observe contention
    /// without ever hanging.
    /// </summary>
    private static async Task<bool> TryAcquireBillSettlementLockAsync(Npgsql.NpgsqlConnection connection, Guid billId)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT pg_try_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"bill-settlement:{billId:N}");
        var acquired = (bool)(await command.ExecuteScalarAsync())!;
        await transaction.CommitAsync();
        return acquired;
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
