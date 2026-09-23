using ALKAROS.TestHelpers;

namespace ALKAROS.Reporting.Payments.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-RPT-001 and applies outbox,
/// catalog, tables, orders, billing, reconciliation-cases, payments (120,
/// 121), cash-sessions (122), payment-allocations (123), cash-transactions
/// (124) and card-settlement-attempts (140) migrations in order — the
/// settlement report reads across all of these real, already-Done schemas.
/// </summary>
public sealed class PaymentReportTestDatabase : PgTestDatabase
{
    public PaymentReportTestDatabase()
        : base("alkaros_rpt001_")
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

    /// <summary>Seeds a minimal billing.bills row (no table/order needed) and returns its id.</summary>
    public async Task<Guid> SeedBillAsync(decimal payableAmount = 100m)
    {
        var billId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO billing.bills
                (bill_id, bill_number, status, payable_amount, currency_code, opened_at, created_at, updated_at)
            VALUES
                (@bill_id, @bill_number, 'Open', @payable_amount, 'TRY', now(), now(), now());
            """,
            ("bill_id", billId),
            ("bill_number", $"B-{billId:N}"[..20]),
            ("payable_amount", payableAmount));
        return billId;
    }

    /// <summary>Seeds a payments.payments row with the given status/amount/timestamp and returns its id.</summary>
    public async Task<Guid> SeedPaymentAsync(
        Guid billId, string status, decimal amount, DateTimeOffset at, decimal? approvedAmount = null)
    {
        var paymentId = Guid.NewGuid();
        var tendered = status == "Initiated" ? (decimal?)null : amount;
        var approved = status == "Approved" ? (approvedAmount ?? amount) : (decimal?)null;
        await ExecuteAsync(
            """
            INSERT INTO payments.payments
                (payment_id, bill_id, status, currency_code, requested_amount, tendered_amount, approved_amount,
                 change_amount, initiated_at, tendered_at, approved_at, created_at, updated_at)
            VALUES
                (@payment_id, @bill_id, @status, 'TRY', @amount, @tendered, @approved,
                 0, @at, @tendered_at, @approved_at, @at, @at);
            """,
            ("payment_id", paymentId),
            ("bill_id", billId),
            ("status", status),
            ("amount", amount),
            ("tendered", (object?)tendered ?? DBNull.Value),
            ("approved", (object?)approved ?? DBNull.Value),
            ("at", at.UtcDateTime),
            ("tendered_at", (object?)(tendered is not null ? at.UtcDateTime : null) ?? DBNull.Value),
            ("approved_at", (object?)(approved is not null ? at.UtcDateTime : null) ?? DBNull.Value));
        return paymentId;
    }

    public async Task SeedCashSaleTransactionAsync(Guid cashSessionId, Guid paymentId, decimal amount)
    {
        await ExecuteAsync(
            """
            INSERT INTO cash.cash_transactions
                (cash_transaction_id, cash_session_id, type, direction, amount, related_payment_id, occurred_at)
            VALUES
                (@id, @session, 'Sale', 'In', @amount, @payment, now());
            """,
            ("id", Guid.NewGuid()),
            ("session", cashSessionId),
            ("amount", amount),
            ("payment", paymentId));
    }

    public async Task<Guid> SeedCashSessionAsync(
        Guid terminalId, string status, decimal expectedCash, decimal actualCash, DateTimeOffset openedAt)
    {
        var sessionId = Guid.NewGuid();
        var closedAt = status is "Closed" or "Reconciled" ? (DateTimeOffset?)openedAt.AddHours(8) : null;
        await ExecuteAsync(
            """
            INSERT INTO cash.cash_sessions
                (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, expected_cash, actual_cash,
                 difference, opened_at, closed_at, created_at, updated_at)
            VALUES
                (@id, @cashier, @terminal, @status, @opening, @expected, @actual,
                 @difference, @opened_at, @closed_at, @opened_at, @opened_at);
            """,
            ("id", sessionId),
            ("cashier", Guid.NewGuid()),
            ("terminal", terminalId),
            ("status", status),
            ("opening", 0m),
            ("expected", expectedCash),
            ("actual", actualCash),
            ("difference", actualCash - expectedCash),
            ("opened_at", openedAt.UtcDateTime),
            ("closed_at", (object?)closedAt?.UtcDateTime ?? DBNull.Value));
        return sessionId;
    }

    /// <summary>
    /// Seeds a real Approved card_settlement_attempts row. Its own CHECK
    /// constraint (<c>ck_card_settlement_attempts_approved_fields</c>)
    /// requires a real allocation_id and fiscal_handoff_queued=true for any
    /// Approved outcome, so this also seeds the matching
    /// payment_allocations row rather than a bare attempts row.
    /// </summary>
    public async Task SeedCardSettlementAttemptAsync(Guid paymentId, Guid billId, decimal approvedAmount)
    {
        var allocationId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO payments.payment_allocations
                (payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES
                (@allocation_id, @payment, @bill, @amount, 'TRY', @idempotency_alloc, now());
            """,
            ("allocation_id", allocationId),
            ("payment", paymentId),
            ("bill", billId),
            ("amount", approvedAmount),
            ("idempotency_alloc", $"rpt-test-alloc-{Guid.NewGuid():N}"));

        await ExecuteAsync(
            """
            INSERT INTO payments.card_settlement_attempts
                (card_settlement_attempt_id, idempotency_key, provider_correlation_id, payment_id, outcome,
                 approved_amount, allocation_id, fiscal_handoff_queued, created_at)
            VALUES
                (@id, @idempotency, @correlation, @payment, 'Approved', @amount, @allocation_id, true, now());
            """,
            ("id", Guid.NewGuid()),
            ("idempotency", $"rpt-test-{Guid.NewGuid():N}"),
            ("correlation", $"corr-{Guid.NewGuid():N}"),
            ("payment", paymentId),
            ("amount", approvedAmount),
            ("allocation_id", allocationId));
    }

    public async Task SeedReconciliationCaseAsync(
        string caseType, string status, decimal discrepancyAmount, DateTimeOffset openedAt)
    {
        await ExecuteAsync(
            """
            INSERT INTO reconciliation.cases
                (case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity,
                 status, opened_at)
            VALUES
                (@id, @dedup, @case_type, 'src-a', 'src-b', @amount, 'Medium', @status, @opened_at);
            """,
            ("id", Guid.NewGuid()),
            ("dedup", $"rpt-test-{Guid.NewGuid():N}"),
            ("case_type", caseType),
            ("amount", discrepancyAmount),
            ("status", status),
            ("opened_at", openedAt.UtcDateTime));
    }
}
