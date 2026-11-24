using Npgsql;

namespace ALKAROS.Reporting.Payments;

/// <summary>Postgres-backed <see cref="IPaymentSettlementReportRepository"/> (V13-RPT-001). Read-only.</summary>
public sealed class PostgresPaymentSettlementReportRepository : IPaymentSettlementReportRepository
{
    // A business date's cash-session and reconciliation rows are
    // bounded by real-world daily volume (see V13-RMD-087's own load-test
    // baseline). A single-day report outgrowing this is itself a signal
    // something is wrong (a stuck window, a filter bug) — fail loud rather
    // than silently truncate.
    private const int MaxScanRows = 20_000;

    // MethodSql has exactly five possible results, so a method grouping can never return more rows than this.
    private const int MethodCount = 5;

    // The payments table stores no method, so it is read from the records each way leaves behind. Same rule as
    // Host PaymentMethodLookup (modules cannot reference the Host): an account charge, a cash sale, an approved card
    // attempt or an approved manual card confirmation, a card payment taken on a bank POS (its slip receipt); transfer is what is left because the producers of approved
    // payments are a closed set. EXISTS keeps one row per payment however many attempts it has, and a declined or
    // pending attempt does not make a card payment.
    private const string MethodSql = """
        CASE
            WHEN EXISTS (SELECT 1 FROM customer_account.account_transactions a
                         WHERE a.source_reference_type = 'Payment' AND a.source_reference_id = p.payment_id
                           AND a.transaction_type = 'Charge') THEN 'CustomerAccount'
            WHEN EXISTS (SELECT 1 FROM cash.cash_transactions c
                         WHERE c.related_payment_id = p.payment_id AND c.type = 'Sale') THEN 'Cash'
            WHEN EXISTS (SELECT 1 FROM payments.card_settlement_attempts s
                         WHERE s.payment_id = p.payment_id AND s.outcome = 'Approved')
              OR EXISTS (SELECT 1 FROM payments.manual_card_confirmations m
                         WHERE m.payment_id = p.payment_id AND m.status = 'Approved') THEN 'BankCard'
            WHEN EXISTS (SELECT 1 FROM payments.external_card_receipts x
                         WHERE x.payment_id = p.payment_id) THEN 'ExternalCard'
            ELSE 'Eft' END
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPaymentSettlementReportRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<PaymentMixEntry>> GetPaymentMixAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        var results = new List<PaymentMixEntry>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {MethodSql} AS method, COUNT(*) AS approved_count, COALESCE(SUM(p.approved_amount), 0) AS approved_amount
            FROM payments.payments p
            WHERE p.status IN ('Approved', 'Refunded', 'PartiallyRefunded')
              AND p.approved_at >= $1 AND p.approved_at < $2
            GROUP BY 1
            LIMIT {MethodCount};
            """);
        command.Parameters.AddWithValue(windowStart.UtcDateTime);
        command.Parameters.AddWithValue(windowEnd.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new PaymentMixEntry(
                reader.GetString(0),
                (int)reader.GetInt64(1),
                reader.GetDecimal(2)));
        }

        // A refund lowers the method's amount on the day it was paid back, not on the day of the original payment; the count
        // drops by one on the day the refunds reach the whole approved amount, so count and amount stay net of the same payments.
        await using var refunds = _dataSource.CreateCommand(
            $"""
            SELECT method, COALESCE(SUM(refunded), 0) AS refunded_amount, COUNT(*) FILTER (WHERE completed) AS completed_count
            FROM (
                SELECT {MethodSql} AS method,
                       SUM(r.amount) FILTER (WHERE r.occurred_at >= $1) AS refunded,
                       SUM(r.amount) >= p.approved_amount AS completed
                FROM payments.payment_reversals r
                JOIN payments.payments p ON p.payment_id = r.payment_id
                WHERE r.occurred_at < $2
                  AND r.payment_id IN (SELECT payment_id FROM payments.payment_reversals WHERE occurred_at >= $1 AND occurred_at < $2)
                GROUP BY p.payment_id
            ) per_payment
            GROUP BY method
            LIMIT {MethodCount};
            """);
        refunds.Parameters.AddWithValue(windowStart.UtcDateTime);
        refunds.Parameters.AddWithValue(windowEnd.UtcDateTime);
        await using (var refundReader = await refunds.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await refundReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var method = refundReader.GetString(0);
                var refunded = refundReader.GetDecimal(1);
                var wholly = (int)refundReader.GetInt64(2);
                var index = results.FindIndex(entry => entry.Method == method);
                if (index >= 0)
                    results[index] = results[index] with { ApprovedCount = results[index].ApprovedCount - wholly, ApprovedAmount = results[index].ApprovedAmount - refunded };
                else
                    results.Add(new PaymentMixEntry(method, -wholly, -refunded));
            }
        }

        return results;
    }

    public async Task<UnsettledPaymentSummary> GetUnsettledPaymentsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT
                COALESCE(SUM(CASE WHEN status = 'Unknown' THEN 1 ELSE 0 END), 0) AS unknown_count,
                COALESCE(SUM(CASE WHEN status = 'Unknown' THEN tendered_amount ELSE 0 END), 0) AS unknown_amount,
                COALESCE(SUM(CASE WHEN status = 'ReconciliationRequired' THEN 1 ELSE 0 END), 0) AS rr_count,
                COALESCE(SUM(CASE WHEN status = 'ReconciliationRequired' THEN tendered_amount ELSE 0 END), 0) AS rr_amount
            FROM payments.payments
            WHERE status IN ('Unknown', 'ReconciliationRequired')
              AND initiated_at >= $1 AND initiated_at < $2;
            """);
        command.Parameters.AddWithValue(windowStart.UtcDateTime);
        command.Parameters.AddWithValue(windowEnd.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        return new UnsettledPaymentSummary(
            (int)reader.GetInt64(0),
            reader.GetDecimal(1),
            (int)reader.GetInt64(2),
            reader.GetDecimal(3));
    }

    public async Task<IReadOnlyList<CashSessionSummaryEntry>> GetCashSessionsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, Guid? terminalId, CancellationToken cancellationToken = default)
    {
        var results = new List<CashSessionSummaryEntry>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT s.cash_session_id, s.terminal_id, s.status, s.expected_cash, s.actual_cash, s.difference,
                   (c.state->>'firstCountedAmount')::numeric, (c.state->>'countEntries')::int,
                   (c.state->>'drawerSaleOpenCount')::int, (c.state->>'drawerNoSaleCount')::int,
                   (c.state->>'drawerNoSaleKickFailedCount')::int, (c.state->>'drawerSaleKickFailedCount')::int
            FROM cash.cash_sessions s
            LEFT JOIN LATERAL (
                SELECT a.after_state_json AS state FROM audit.audit_events a
                WHERE a.aggregate_type = 'CashSession' AND a.aggregate_id = s.cash_session_id AND a.event_name = 'cash.session.closed'
                ORDER BY a.occurred_at DESC LIMIT 1) c ON true
            WHERE s.opened_at >= $1 AND s.opened_at < $2
              AND ($3::uuid IS NULL OR s.terminal_id = $3)
            ORDER BY s.opened_at
            LIMIT {MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(windowStart.UtcDateTime);
        command.Parameters.AddWithValue(windowEnd.UtcDateTime);
        command.Parameters.AddWithValue((object?)terminalId ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var status = reader.GetString(2);
            results.Add(new CashSessionSummaryEntry(
                reader.GetGuid(0),
                reader.GetGuid(1),
                status,
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                IsOpen: status is "Open" or "Counting" or "Closing",
                FirstCountedAmount: reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                CorrectionCount: reader.IsDBNull(7) ? null : Math.Max(reader.GetInt32(7) - 1, 0),
                SaleOpenCount: reader.IsDBNull(8) ? null : reader.GetInt32(8),
                NoSaleCount: reader.IsDBNull(9) ? null : reader.GetInt32(9),
                NoSaleKickFailedCount: reader.IsDBNull(10) ? null : reader.GetInt32(10),
                SaleKickFailedCount: reader.IsDBNull(11) ? null : reader.GetInt32(11)));
        }

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"Cash session report returned more than {MaxScanRows} rows; narrow the filter.");

        return results;
    }

    public async Task<IReadOnlyList<ReconciliationTotalsEntry>> GetReconciliationTotalsAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        var results = new List<ReconciliationTotalsEntry>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT
                case_type,
                COALESCE(SUM(CASE WHEN status IN ('Open', 'Investigating', 'Escalated') THEN 1 ELSE 0 END), 0) AS open_count,
                COALESCE(SUM(CASE WHEN status IN ('Resolved', 'Dismissed') THEN 1 ELSE 0 END), 0) AS resolved_count,
                COALESCE(SUM(CASE WHEN status IN ('Open', 'Investigating', 'Escalated') THEN discrepancy_amount ELSE 0 END), 0) AS open_amount
            FROM reconciliation.cases
            WHERE opened_at >= $1 AND opened_at < $2
            GROUP BY case_type
            LIMIT {MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(windowStart.UtcDateTime);
        command.Parameters.AddWithValue(windowEnd.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ReconciliationTotalsEntry(
                reader.GetString(0),
                (int)reader.GetInt64(1),
                (int)reader.GetInt64(2),
                reader.GetDecimal(3)));
        }

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"Reconciliation totals report returned more than {MaxScanRows} groups; narrow the filter.");

        return results;
    }
}
