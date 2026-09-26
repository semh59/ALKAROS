using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// The stock outcome side (V12-ONL-005): a channel still shows a sellable quantity other than the one
/// this restaurant can actually hold, and the difference has lasted past the tolerance or survived
/// several failed deliveries — the channel may keep selling what is gone. The publisher itself retries
/// every round, so the case's next action is to check the channel's connection; the divergence ends when
/// the delivered quantity equals the desired one.
/// </summary>
public sealed class AvailabilityNotDeliveredSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-availability:";
    public const int ToleranceMinutes = 10;
    public const int FailedAttempts = 3;

    private readonly NpgsqlDataSource _dataSource;

    public AvailabilityNotDeliveredSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.AvailabilityNotDelivered;
    public string Kind => OnlineOrderDivergenceKind.AvailabilityNotDelivered;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT s.channel, s.product_id, s.external_sku
            FROM online_ordering.availability_states s
            WHERE s.delivered_quantity IS DISTINCT FROM s.desired_quantity
              AND (s.desired_at < now() - make_interval(mins => $1) OR s.delivery_attempts >= $2)
              -- V12-RMD-006: a dismissal silences the divergence a person looked at; a new quantity after it
              -- (desired_at moves only when the quantity changes) is a new divergence and opens a new case.
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $3 || s.channel || ':' || s.product_id::text
                                AND rc.status = 'Dismissed' AND rc.resolved_at >= s.desired_at)
            ORDER BY s.channel, s.product_id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(ToleranceMinutes);
        command.Parameters.AddWithValue(FailedAttempts);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var channel = reader.GetString(0);
            var productId = reader.GetGuid(1);
            var sku = reader.GetString(2);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.CheckChannelConnection, Channel: channel, ProductId: productId);
            return new DetectedDiscrepancy(
                $"{DeduplicationPrefix}{channel}:{productId}",
                CaseType.OnlineOrderMismatch,
                $"online_ordering.availability_states:{channel}:{productId}",
                $"{channel}:sku:{sku}",
                0m,
                CaseSeverity.Medium,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
