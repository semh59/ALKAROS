using ALKAROS.Reconciliation.CaseFoundation;
using Npgsql;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// "Cash discrepancy" source pair (V13-REC-001 In scope):
/// <c>cash.cash_sessions</c> (V13-CSH-001, Done) already computes and
/// stores <c>expected_cash</c>/<c>actual_cash</c>/<c>difference</c> at
/// session close time (the physical count vs. the session's own expected
/// total) — this source pair does not recompute cash accounting, it only
/// surfaces a Closed/Reconciled session whose own already-recorded
/// difference is non-zero as a reconciliation case, so a real drawer
/// shortfall/overage is never left buried in a closed session's history
/// without a tracked, auditable case.
/// </summary>
public sealed class CashSessionDifferenceSourcePair : IReconciliationSourcePair
{
    // See ApprovedWithoutAllocationSourcePair's own comment on this bound.
    private const int MaxScanRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public CashSessionDifferenceSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => "CashDifference";
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedDiscrepancy>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT cash_session_id, terminal_id, expected_cash, actual_cash, difference
            FROM cash.cash_sessions
            WHERE status IN ('Closed', 'Reconciled')
              AND difference <> 0
            LIMIT {MaxScanRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var sessionId = reader.GetGuid(0);
            var terminalId = reader.GetGuid(1);
            var expectedCash = reader.GetDecimal(2);
            var actualCash = reader.GetDecimal(3);
            var difference = reader.GetDecimal(4);

            results.Add(new DetectedDiscrepancy(
                DeduplicationKey: $"cash-difference:{sessionId}",
                CaseType: CaseType.CashVariance,
                SourceARef: $"cash.cash_sessions:{sessionId}:expected",
                SourceBRef: $"cash.cash_sessions:{sessionId}:actual",
                DiscrepancyAmount: Math.Abs(difference),
                Severity: Math.Abs(difference) >= 100m ? CaseSeverity.High : CaseSeverity.Medium,
                DetailsJson: $$"""{"cashSessionId":"{{sessionId}}","terminalId":"{{terminalId}}","expectedCash":{{expectedCash.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"actualCash":{{actualCash.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"difference":{{difference.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""));
        }

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"CashDifference scan returned more than {MaxScanRows} rows; narrow the filter or paginate.");

        return results;
    }
}
