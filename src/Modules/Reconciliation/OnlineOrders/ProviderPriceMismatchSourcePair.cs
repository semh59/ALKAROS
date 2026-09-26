using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// V12-RMD-008: a provider order accepted locally with item prices that differ from the products' catalog prices
/// (a stale catalog on the provider side, a provider-side price change, or a promotion). The order was not stopped
/// and was recorded at the provider's prices; the amount is the sum of |provider - catalog| x quantity. The source
/// can never show it ending, so a person resolves it with a recorded decision and it is never opened again.
/// </summary>
public sealed class ProviderPriceMismatchSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:provider-price-mismatch:";

    private readonly NpgsqlDataSource _dataSource;

    public ProviderPriceMismatchSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.ProviderPriceMismatch;
    public string Kind => OnlineOrderDivergenceKind.ProviderPriceMismatch;
    public bool RequiresManualResolution => true;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT i.external_order_id, i.order_id, (i.outcome_detail->>'priceDifferenceAmount')::numeric
            FROM online_ordering.yemeksepeti_webhook_inbox i
            WHERE i.processing_outcome = 'OrderCreated'
              AND i.outcome_detail->>'pricesMatch' = 'false'
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
            var amount = reader.GetDecimal(2);
            var details = new OnlineOrderCaseDetails(
                Kind, OnlineOrderNextAction.SettleWithProvider, ExternalOrderId: externalOrderId, OrderId: orderId);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + externalOrderId,
                CaseType.OnlineOrderMismatch,
                $"orders.orders:{orderId}",
                $"yemeksepeti:order:{externalOrderId}",
                amount,
                CaseSeverity.Medium,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
