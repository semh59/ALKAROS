using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// An accepted online order that nobody handed over or cancelled for hours. Its stock stays held and no
/// consumption is written until staff finish it, so it is shown as a case instead of being closed by the
/// system (only the restaurant knows what really happened to the food). Handing the order over or cancelling
/// it takes it out of this source, which resolves the case on the next check; a dismissed case is not reopened.
/// </summary>
public sealed class NotHandedOverSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:not-handed-over:";

    /// <summary>How long an online order may stay open before it is reported.</summary>
    public static readonly TimeSpan OpenLimit = TimeSpan.FromHours(3);

    private readonly NpgsqlDataSource _dataSource;

    public NotHandedOverSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.NotHandedOver;
    public string Kind => OnlineOrderDivergenceKind.NotHandedOver;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT o.order_id, l.provider, l.external_order_id, o.total
            FROM orders.orders o
            JOIN online_ordering.online_orders l ON l.order_id = o.order_id
            WHERE o.source = 'Online'
              AND o.status IN ('Accepted', 'Preparing', 'Ready')
              AND o.created_at < now() - $1
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $2 || l.provider || ':' || l.external_order_id
                                AND rc.status = 'Dismissed')
            ORDER BY o.created_at, o.order_id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(OpenLimit);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var orderId = reader.GetGuid(0);
            var provider = reader.GetString(1);
            var externalOrderId = reader.GetString(2);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.HandOverOrCancelOrder,
                ExternalOrderId: externalOrderId, OrderId: orderId, Provider: provider);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + provider + ":" + externalOrderId,
                CaseType.OnlineOrderMismatch,
                $"orders.orders:{orderId}",
                $"{provider}:order:{externalOrderId}",
                reader.GetDecimal(3),
                CaseSeverity.High,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
