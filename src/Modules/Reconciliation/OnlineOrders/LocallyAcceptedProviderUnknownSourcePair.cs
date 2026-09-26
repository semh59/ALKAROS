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
                SELECT id, created_at, convert_from(payload_envelope, 'UTF8')::jsonb->>'externalOrderId' AS external_order_id
                FROM outbox_messages
                WHERE event_type = $1 AND status = 'dead')
            SELECT d.id, o.source_external_id, o.order_id, o.total
            FROM dead_updates d
            JOIN orders.orders o ON o.source = 'Online' AND o.source_external_id = d.external_order_id
            ORDER BY d.created_at, d.id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(OnlineOrderSourceScan.StatusUpdateEventType);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var messageId = reader.GetGuid(0);
            var externalOrderId = reader.GetString(1);
            var orderId = reader.GetGuid(2);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.ResendProviderUpdate,
                ExternalOrderId: externalOrderId, OrderId: orderId, OutboxMessageId: messageId);
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
