using Npgsql;

namespace ALKAROS.Reporting.Payments;

/// <summary>Postgres-backed <see cref="IPaymentSettlementReportRepository"/> (V13-RPT-001). Read-only.</summary>
public sealed class PostgresPaymentSettlementReportRepository : IPaymentSettlementReportRepository
{
    // A business date's payment-mix/cash-session/reconciliation rows are
    // bounded by real-world daily volume (see V13-RMD-087's own load-test
    // baseline). A single-day report outgrowing this is itself a signal
    // something is wrong (a stuck window, a filter bug) — fail loud rather
    // than silently truncate.
    private const int MaxScanRows = 20_000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPaymentSettlementReportRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<PaymentMixEntry>> GetPaymentMixAsync(
        DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        var results = new List<PaymentMixEntry>();

        // Method inference by elimination (see PaymentMixEntry's own doc
        // comment for why no column directly stores it): a Sale-typed cash
        // transaction linked to the payment means Cash; a
        // card_settlement_attempts row means BankCard; neither means Eft —
        // the only other method V13-PAY-003's registry actually enables
        // today.
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT
                CASE
                    WHEN cash_tx.related_payment_id IS NOT NULL THEN 'Cash'
                    WHEN csa.payment_id IS NOT NULL THEN 'BankCard'
                    ELSE 'Eft'
                END AS method,
                COUNT(*) AS approved_count,
                COALESCE(SUM(p.approved_amount), 0) AS approved_amount
            FROM payments.payments p
            LEFT JOIN cash.cash_transactions cash_tx
                ON cash_tx.related_payment_id = p.payment_id AND cash_tx.type = 'Sale'
            LEFT JOIN payments.card_settlement_attempts csa
                ON csa.payment_id = p.payment_id
            WHERE p.status = 'Approved'
              AND p.approved_at >= $1 AND p.approved_at < $2
            GROUP BY
                CASE
                    WHEN cash_tx.related_payment_id IS NOT NULL THEN 'Cash'
                    WHEN csa.payment_id IS NOT NULL THEN 'BankCard'
                    ELSE 'Eft'
                END
            LIMIT {MaxScanRows + 1};
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

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"Payment mix report returned more than {MaxScanRows} groups; narrow the filter.");

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
            SELECT cash_session_id, terminal_id, status, expected_cash, actual_cash, difference
            FROM cash.cash_sessions
            WHERE opened_at >= $1 AND opened_at < $2
              AND ($3::uuid IS NULL OR terminal_id = $3)
            ORDER BY opened_at
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
                IsOpen: status is "Open" or "Counting" or "Closing"));
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
