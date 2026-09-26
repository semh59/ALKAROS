using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// Provider accepted / locally refused (V12-REC-001 Acceptance evidence): Yemeksepeti sent an order that
/// intake refused (an unmapped item, or stock it could not hold), so no local order exists while the
/// provider may still expect one. The divergence ends when the order exists locally after all (the event
/// was reprocessed), the provider's own cancellation arrived, or our cancellation actually reached the
/// provider (its outbox message was dispatched). A case a person dismissed is not opened again.
/// </summary>
public sealed class ProviderAcceptedLocallyRefusedSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:provider-accepted-locally-refused:";

    private readonly NpgsqlDataSource _dataSource;

    public ProviderAcceptedLocallyRefusedSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.ProviderAcceptedLocallyRefused;
    public string Kind => OnlineOrderDivergenceKind.ProviderAcceptedLocallyRefused;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            WITH status_updates AS MATERIALIZED (
                SELECT status, convert_from(payload_envelope, 'UTF8')::jsonb->>'externalOrderId' AS external_order_id,
                       {OnlineOrderSourceScan.ProviderOfEventTypeSql} AS provider
                FROM outbox_messages
                WHERE event_type = ANY($1))
            SELECT DISTINCT ON (i.provider, i.external_order_id)
                   i.inbox_id, i.external_order_id,
                   COALESCE((i.outcome_detail->>'providerCancellationRequested')::boolean, false),
                   COALESCE(i.outcome_detail->>'rejection', i.outcome_detail->>'reason'),
                   i.provider
            FROM online_ordering.provider_inbox i
            WHERE i.processing_outcome IN ('Rejected', 'Diverged')
              AND i.order_id IS NULL
              AND NOT EXISTS (SELECT 1 FROM online_ordering.online_orders l
                              WHERE l.provider = i.provider AND l.external_order_id = i.external_order_id)
              AND NOT EXISTS (SELECT 1 FROM online_ordering.provider_inbox c
                              WHERE c.provider = i.provider AND c.external_order_id = i.external_order_id
                                AND c.processing_outcome = 'CancelledBeforeOrder')
              -- V12-TGO-003: the cancellation this platform was sent was delivered (any platform's own update queue).
              AND NOT EXISTS (SELECT 1 FROM status_updates u
                              WHERE u.provider = i.provider AND u.status = 'dispatched' AND u.external_order_id = i.external_order_id)
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $2 || i.provider || ':' || i.external_order_id
                                AND rc.status = 'Dismissed')
            ORDER BY i.provider, i.external_order_id, i.received_at DESC
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(OnlineOrderSourceScan.StatusUpdateEventTypes);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var inboxId = reader.GetGuid(0);
            var externalOrderId = reader.GetString(1);
            var cancellationRequested = reader.GetBoolean(2);
            var provider = reader.GetString(4);
            var details = new OnlineOrderCaseDetails(
                Kind,
                cancellationRequested ? OnlineOrderNextAction.ResendProviderCancellation : OnlineOrderNextAction.ReprocessProviderEvent,
                ExternalOrderId: externalOrderId,
                InboxId: inboxId,
                Reason: reader.IsDBNull(3) ? null : reader.GetString(3),
                Provider: provider);
            return new DetectedDiscrepancy(
                $"{DeduplicationPrefix}{provider}:{externalOrderId}",
                CaseType.OnlineOrderMismatch,
                $"online_ordering.provider_inbox:{inboxId}",
                $"{provider}:order:{externalOrderId}",
                0m,
                CaseSeverity.High,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
