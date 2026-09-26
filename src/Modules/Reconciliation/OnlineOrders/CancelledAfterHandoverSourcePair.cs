using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// The provider cancelled an order after it had been handed over locally (V12-ONL-003 records it as a
/// <c>Diverged</c> event with a stable evidence id). The food left the restaurant, so nothing can be
/// retried: the case stays open until a person records how it was settled with the provider, and a case
/// once resolved or dismissed is never opened again for the same evidence.
/// </summary>
public sealed class CancelledAfterHandoverSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:cancelled-after-handover:";

    private readonly NpgsqlDataSource _dataSource;

    public CancelledAfterHandoverSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.CancelledAfterHandover;
    public string Kind => OnlineOrderDivergenceKind.CancelledAfterHandover;
    public bool RequiresManualResolution => true;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT DISTINCT ON (i.outcome_detail->>'evidenceId')
                   i.outcome_detail->>'evidenceId', i.external_order_id, i.order_id, o.total, i.provider
            FROM online_ordering.provider_inbox i
            JOIN orders.orders o ON o.order_id = i.order_id
            WHERE i.processing_outcome = 'Diverged'
              AND i.outcome_detail->>'reason' = 'CancelledAfterHandover'
              AND i.outcome_detail->>'evidenceId' IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $1 || (i.outcome_detail->>'evidenceId')
                                AND rc.status IN ('Resolved', 'Dismissed'))
            ORDER BY i.outcome_detail->>'evidenceId', i.received_at
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var evidenceId = reader.GetString(0);
            var externalOrderId = reader.GetString(1);
            var orderId = reader.GetGuid(2);
            var provider = reader.GetString(4);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.SettleWithProvider,
                ExternalOrderId: externalOrderId, OrderId: orderId, Reason: evidenceId, Provider: provider);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + evidenceId,
                CaseType.OnlineOrderMismatch,
                $"orders.orders:{orderId}",
                $"{provider}:order:{externalOrderId}",
                reader.GetDecimal(3),
                CaseSeverity.Critical,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
