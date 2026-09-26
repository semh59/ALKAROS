using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// Locally accepted / provider unknown (V12-REC-001 Acceptance evidence): a local online order moved on
/// (handed over, cancelled by the restaurant) but the status update meant for the provider exhausted its
/// outbox retries and is dead, so the provider does not know. One case per undelivered update; the
/// divergence ends when that message has been dispatched.
/// </summary>
public sealed class LocallyAcceptedProviderUnknownSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:provider-unknown:";

    private readonly NpgsqlDataSource _dataSource;

    public LocallyAcceptedProviderUnknownSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.LocallyAcceptedProviderUnknown;
    public string Kind => OnlineOrderDivergenceKind.LocallyAcceptedProviderUnknown;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            WITH dead_updates AS MATERIALIZED (
                SELECT id, created_at, convert_from(payload_envelope, 'UTF8')::jsonb->>'externalOrderId' AS external_order_id,
                       {OnlineOrderSourceScan.ProviderOfEventTypeSql} AS provider
                FROM outbox_messages
                WHERE event_type = ANY($1) AND status = 'dead')
            SELECT d.id, l.external_order_id, o.order_id, o.total, l.provider
            FROM dead_updates d
            -- V12-TGO-003: every platform's dead status updates, each matched to its own platform's order.
            JOIN online_ordering.online_orders l ON l.provider = d.provider AND l.external_order_id = d.external_order_id
            JOIN orders.orders o ON o.order_id = l.order_id
            -- V12-RMD-006: a person dismissed this update's case; it is not reopened on every scan.
            WHERE NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $2 || d.id::text AND rc.status = 'Dismissed')
            ORDER BY d.created_at, d.id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(OnlineOrderSourceScan.StatusUpdateEventTypes);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var messageId = reader.GetGuid(0);
            var externalOrderId = reader.GetString(1);
            var orderId = reader.GetGuid(2);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.ResendProviderUpdate,
                ExternalOrderId: externalOrderId, OrderId: orderId, OutboxMessageId: messageId, Provider: reader.GetString(4));
            return new DetectedDiscrepancy(
                DeduplicationPrefix + messageId,
                CaseType.OnlineOrderMismatch,
                $"orders.orders:{orderId}",
                $"outbox_messages:{messageId}",
                reader.GetDecimal(3),
                CaseSeverity.High,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
