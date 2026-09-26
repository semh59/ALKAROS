using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// V12-RMD-004: a provider order accepted locally whose <c>payment.sub_total</c> differed from the lines' own
/// total (a discount, a fee or a changed item the local order does not carry). The order was not stopped; the
/// difference is what the provider will settle versus what the restaurant recorded. The source can never show
/// it ending, so a person resolves it with a recorded decision and it is never opened again.
/// </summary>
public sealed class ProviderTotalMismatchSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:provider-total-mismatch:";

    private readonly NpgsqlDataSource _dataSource;

    public ProviderTotalMismatchSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.ProviderTotalMismatch;
    public string Kind => OnlineOrderDivergenceKind.ProviderTotalMismatch;
    public bool RequiresManualResolution => true;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT i.external_order_id, i.order_id,
                   (i.outcome_detail->>'providerSubTotal')::numeric, (i.outcome_detail->>'localSubTotal')::numeric
            FROM online_ordering.provider_inbox i
            WHERE i.processing_outcome = 'OrderCreated'
              AND i.outcome_detail->>'totalsMatch' = 'false'
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $1 || i.external_order_id
                                AND rc.status IN ('Resolved', 'Dismissed'))
            ORDER BY i.received_at, i.inbox_id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var externalOrderId = reader.GetString(0);
            var orderId = reader.GetGuid(1);
            var provider = reader.GetDecimal(2);
            var local = reader.GetDecimal(3);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.SettleWithProvider, ExternalOrderId: externalOrderId, OrderId: orderId);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + externalOrderId,
                CaseType.OnlineOrderMismatch,
                $"orders.orders:{orderId}",
                $"yemeksepeti:order:{externalOrderId}",
                Math.Abs(provider - local),
                CaseSeverity.Medium,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
