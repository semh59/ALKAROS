using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// A stored provider event whose processing kept failing and was closed as <c>Failed</c> (V12-ONL-002's
/// bounded retries): whatever the provider said — a new order, a cancellation — never reached the local
/// side. The divergence ends when the event has been processed again with any other outcome.
/// </summary>
public sealed class ProviderEventFailedSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:provider-event-failed:";

    private readonly NpgsqlDataSource _dataSource;

    public ProviderEventFailedSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.ProviderEventFailed;
    public string Kind => OnlineOrderDivergenceKind.ProviderEventFailed;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT i.inbox_id, i.external_order_id
            FROM online_ordering.yemeksepeti_webhook_inbox i
            WHERE i.processing_outcome = 'Failed'
              -- V12-RMD-006: a person dismissed this event's case; it is not reopened on every scan.
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $1 || i.inbox_id::text AND rc.status = 'Dismissed')
            ORDER BY i.received_at, i.inbox_id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var inboxId = reader.GetGuid(0);
            var externalOrderId = reader.GetString(1);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.ReprocessProviderEvent, ExternalOrderId: externalOrderId, InboxId: inboxId);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + inboxId,
                CaseType.OnlineOrderMismatch,
                $"online_ordering.yemeksepeti_webhook_inbox:{inboxId}",
                $"yemeksepeti:order:{externalOrderId}",
                0m,
                CaseSeverity.Medium,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
