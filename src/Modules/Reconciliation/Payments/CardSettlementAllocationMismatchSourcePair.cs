using ALKAROS.Reconciliation.CaseFoundation;
using Npgsql;

namespace ALKAROS.Reconciliation.Payments;

/// <summary>
/// "Allocation/provider mismatch" source pair (V13-REC-001 In scope):
/// compares <c>payments.card_settlement_attempts</c> (V13-PAY-004, Done —
/// the provider's own approved amount for a settlement attempt) against
/// the <c>payments.payment_allocations</c> row it produced. V13-PAY-004's
/// own orchestrator already guards against a MISMATCHED provider
/// correlation reaching this table at attempt time (rejects it as a typed
/// exception before ever writing), so this source pair is a periodic,
/// query-time SECOND check for the narrower case that table's own CHECK
/// constraint cannot express: the allocation row was written correctly at
/// settlement time but has since drifted (e.g. a later, out-of-band
/// correction to the allocation itself) — this scan catches that
/// divergence without needing to touch V13-PAY-004's own Owned surface.
/// </summary>
public sealed class CardSettlementAllocationMismatchSourcePair : IReconciliationSourcePair
{
    // See ApprovedWithoutAllocationSourcePair's own comment on this bound.
    private const int MaxScanRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public CardSettlementAllocationMismatchSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => "AllocationProviderMismatch";
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedDiscrepancy>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT csa.card_settlement_attempt_id, csa.approved_amount, pa.payment_allocation_id, pa.amount
            FROM payments.card_settlement_attempts csa
            JOIN payments.payment_allocations pa ON pa.payment_allocation_id = csa.allocation_id
            WHERE csa.outcome = 'Approved'
              AND csa.approved_amount IS DISTINCT FROM pa.amount
            LIMIT {MaxScanRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var attemptId = reader.GetGuid(0);
            var providerAmount = reader.GetDecimal(1);
            var allocationId = reader.GetGuid(2);
            var allocationAmount = reader.GetDecimal(3);

            results.Add(new DetectedDiscrepancy(
                DeduplicationKey: $"allocation-provider-mismatch:{attemptId}",
                CaseType: CaseType.PaymentMismatch,
                SourceARef: $"payments.card_settlement_attempts:{attemptId}",
                SourceBRef: $"payments.payment_allocations:{allocationId}",
                DiscrepancyAmount: Math.Abs(providerAmount - allocationAmount),
                Severity: CaseSeverity.Critical,
                DetailsJson: $$"""{"cardSettlementAttemptId":"{{attemptId}}","providerApprovedAmount":{{providerAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"allocationId":"{{allocationId}}","allocationAmount":{{allocationAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""));
        }

        if (results.Count > MaxScanRows)
            throw new InvalidOperationException(
                $"AllocationProviderMismatch scan returned more than {MaxScanRows} rows; narrow the filter or paginate.");

        return results;
    }
}
