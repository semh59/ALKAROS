using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// V1-RMD-359 (deep-audit follow-up, 2026-09-27): defense-in-depth for migration 143's own constraint, not the
/// C# layer (which always sets decided_by/decided_at together). ck_manual_card_decided_consistency originally
/// read "(status = 'Pending') = (decided_by IS NULL AND decided_at IS NULL)" - a one-way boolean equality that
/// only forced Pending to have both fields null, never forced Approved/Rejected to have BOTH set together (the
/// exact same defect class fixed for V12-ONL-011's platform_store_status table the same day). A four-eyes
/// payment decision missing who decided or when is exactly the accountability gap this table exists to
/// prevent. A real bill and payment are seeded first so payment_id's foreign key is genuinely satisfied - not
/// bypassed - for every insert this test attempts.
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class ManualCardConfirmationConstraintTests : IAsyncLifetime
{
    private readonly ReconciliationCaseTestDatabase _database = new();
    private Guid _billId;
    private Guid _paymentId;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _billId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO billing.bills (bill_id, bill_number, status, payable_amount, currency_code, opened_at, created_at, updated_at)
            VALUES (@bill_id, @bill_number, 'Open', 100, 'TRY', now(), now(), now());
            """,
            ("bill_id", _billId),
            ("bill_number", $"B-{_billId:N}"[..20]));

        _paymentId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO payments.payments
                (payment_id, bill_id, status, currency_code, requested_amount, initiated_at, created_at, updated_at)
            VALUES (@payment_id, @bill_id, 'Initiated', 'TRY', 100, now(), now(), now());
            """,
            ("payment_id", _paymentId),
            ("bill_id", _billId));
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Theory]
    [InlineData("Approved", true, false)]
    [InlineData("Approved", false, true)]
    [InlineData("Rejected", true, false)]
    [InlineData("Rejected", false, true)]
    public async Task TheDecidedConsistencyConstraintRejectsAStateMissingEitherHalf(string status, bool setDecidedBy, bool setDecidedAt)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(
            """
            INSERT INTO payments.manual_card_confirmations
                (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, decided_by, decided_at)
            VALUES (gen_random_uuid(), @payment_id, @bill_id, 'TEST-SLIP', 100, @status, gen_random_uuid(), now(),
                    @decided_by, @decided_at);
            """,
            ("payment_id", _paymentId),
            ("bill_id", _billId),
            ("status", status),
            ("decided_by", setDecidedBy ? Guid.NewGuid() : DBNull.Value),
            ("decided_at", setDecidedAt ? DateTimeOffset.UtcNow : DBNull.Value)));
        Assert.Equal("23514", ex.SqlState); // check_violation
        Assert.Equal(0, await _database.CountAsync("payments.manual_card_confirmations"));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task TheDecidedConsistencyConstraintAcceptsTheTwoValidCombinations(string status)
    {
        var isPending = status == "Pending";
        var rows = await _database.ExecuteAsync(
            """
            INSERT INTO payments.manual_card_confirmations
                (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, decided_by, decided_at)
            VALUES (gen_random_uuid(), @payment_id, @bill_id, 'TEST-SLIP-OK', 100, @status, gen_random_uuid(), now(),
                    @decided_by, @decided_at);
            """,
            ("payment_id", _paymentId),
            ("bill_id", _billId),
            ("status", status),
            ("decided_by", isPending ? DBNull.Value : Guid.NewGuid()),
            ("decided_at", isPending ? DBNull.Value : DateTimeOffset.UtcNow));
        Assert.Equal(1, rows);
    }
}
