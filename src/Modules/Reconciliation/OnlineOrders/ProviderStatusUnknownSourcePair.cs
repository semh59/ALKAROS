using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// V12-RMD-004: a provider event whose status (or, for a new order, delivery kind) the V12-MAP-002 mapper does not
/// know. Nothing local was done with it — for a RECEIVED that means the provider may count an order the restaurant
/// never saw. Every such event is a case a person settles with the provider; it is never opened again once closed.
/// </summary>
public sealed class ProviderStatusUnknownSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:provider-status-unknown:";

    private readonly NpgsqlDataSource _dataSource;

    public ProviderStatusUnknownSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.ProviderStatusUnknown;
    public string Kind => OnlineOrderDivergenceKind.ProviderStatusUnknown;
    public bool RequiresManualResolution => true;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT i.inbox_id, i.external_order_id, i.provider_status, i.provider
            FROM online_ordering.provider_inbox i
            WHERE i.processing_outcome = 'UnknownStatus'
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $1 || i.inbox_id::text
                                AND rc.status IN ('Resolved', 'Dismissed'))
            ORDER BY i.received_at, i.inbox_id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var inboxId = reader.GetGuid(0);
            var externalOrderId = reader.GetString(1);
            var provider = reader.GetString(3);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.SettleWithProvider,
                ExternalOrderId: externalOrderId, InboxId: inboxId, Reason: reader.GetString(2), Provider: provider);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + inboxId,
                CaseType.OnlineOrderMismatch,
                $"online_ordering.provider_inbox:{inboxId}",
                $"{provider}:order:{externalOrderId}",
                0m,
                CaseSeverity.High,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
