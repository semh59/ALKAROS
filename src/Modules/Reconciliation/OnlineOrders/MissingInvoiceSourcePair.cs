using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// A delivered online order that still has no e-Archive invoice draft after the drafting job had time to make one.
/// The restaurant must invoice the platform user within seven days, so it is shown while time is left. The
/// case names the likely reason (no seller profile yet) and resolves itself once a draft exists; a dismissed
/// case is not reopened. The window matches the drafting job's, so an order older than that is no longer listed.
/// </summary>
public sealed class MissingInvoiceSourcePair : IOnlineOrderSourcePair
{
    public const string DeduplicationPrefix = "online-order:missing-invoice:";

    /// <summary>The drafting job runs every few minutes; an order still without a draft after this is stuck.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(1);

    public static readonly TimeSpan InvoicingWindow = TimeSpan.FromDays(7);

    private readonly NpgsqlDataSource _dataSource;

    public MissingInvoiceSourcePair(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public string Name => OnlineOrderDivergenceKind.MissingInvoice;
    public string Kind => OnlineOrderDivergenceKind.MissingInvoice;
    public bool RequiresManualResolution => false;
    public bool IsEnabled => true;
    public string? DisabledReason => null;

    public async Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT o.order_id, l.provider, l.external_order_id, o.total,
                   EXISTS (SELECT 1 FROM invoicing.seller_profile) AS has_profile
            FROM orders.orders o
            JOIN online_ordering.online_orders l ON l.order_id = o.order_id
            WHERE o.source = 'Online'
              AND o.status IN ('Served', 'Completed')
              AND COALESCE(o.closed_at, o.updated_at) BETWEEN now() - $1 AND now() - $2
              AND NOT EXISTS (SELECT 1 FROM invoicing.order_invoices i WHERE i.order_id = o.order_id)
              AND NOT EXISTS (SELECT 1 FROM reconciliation.cases rc
                              WHERE rc.deduplication_key = $3 || l.provider || ':' || l.external_order_id
                                AND rc.status = 'Dismissed')
            ORDER BY o.created_at, o.order_id
            LIMIT {OnlineOrderSourceScan.MaxScanRows + 1};
            """);
        command.Parameters.AddWithValue(InvoicingWindow);
        command.Parameters.AddWithValue(GracePeriod);
        command.Parameters.AddWithValue(DeduplicationPrefix);

        return await OnlineOrderSourceScan.ReadAsync(command, Name, reader =>
        {
            var orderId = reader.GetGuid(0);
            var provider = reader.GetString(1);
            var externalOrderId = reader.GetString(2);
            var details = new OnlineOrderCaseDetails(
                Kind,
                reader.GetBoolean(4) ? OnlineOrderNextAction.IssueInvoiceManually : OnlineOrderNextAction.EnterSellerProfile,
                ExternalOrderId: externalOrderId, OrderId: orderId, Provider: provider);
            return new DetectedDiscrepancy(
                DeduplicationPrefix + provider + ":" + externalOrderId,
                CaseType.OnlineOrderMismatch,
                $"orders.orders:{orderId}",
                $"{provider}:order:{externalOrderId}",
                reader.GetDecimal(3),
                CaseSeverity.Medium,
                details.ToJson());
        }, cancellationToken).ConfigureAwait(false);
    }
}
