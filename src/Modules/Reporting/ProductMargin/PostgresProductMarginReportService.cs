using Npgsql;

namespace ALKAROS.Reporting.ProductMargin;

public interface IProductMarginReportService
{
    Task<ProductMarginReport> GetReportAsync(ProductMarginFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sold units, VAT-excluded revenue, stock-valued cost and gross margin per product, read-only from the ledgers.
/// Revenue comes from the paid bills' sale lines; cost from the stock the line really consumed (consumption movements minus
/// reversals, so a product mapping and its extras count once each) valued at the weighted average of the stock item's goods
/// receipts up to the service day. A stock item without a receipt history has no cost: the line is flagged, never priced at zero.
/// </summary>
public sealed class PostgresProductMarginReportService : IProductMarginReportService
{
    public const string ReportVersion = "product-margin.v1";
    public const string TimeZoneId = "Europe/Istanbul";

    private const int MaxRows = 1000;

    private const string LinesCte =
        """
        WITH lines AS (
            SELECT bi.order_item_id, bi.product_id, bi.product_name_snapshot, bi.quantity, bi.net_amount, bi.line_type,
                   (b.opened_at AT TIME ZONE 'Europe/Istanbul')::date AS business_date
            FROM billing.bill_items bi
            JOIN billing.bills b ON b.bill_id = bi.bill_id
            WHERE b.status = 'Paid'
              AND bi.line_type IN ('Sale', 'Complimentary')
              AND (b.opened_at AT TIME ZONE 'Europe/Istanbul')::date BETWEEN @from AND @to
        )
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductMarginReportService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ProductMarginReport> GetReportAsync(ProductMarginFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Validate();

        var rows = await ReadRowsAsync(filter, cancellationToken).ConfigureAwait(false);
        if (rows.Count > MaxRows)
            throw new InvalidOperationException($"The product margin report returned more than {MaxRows} rows; narrow the range.");

        var (linesNet, billDiscounts) = await ReadControlTotalsAsync(filter, cancellationToken).ConfigureAwait(false);
        var productNet = rows.Sum(r => r.NetRevenue);
        return new ProductMarginReport(
            ReportVersion,
            filter.From,
            filter.To,
            TimeZoneId,
            rows,
            productNet,
            rows.Sum(r => r.Cost),
            rows.Sum(r => r.UnknownCostLines),
            new ProductMarginCheck(productNet, linesNet, billDiscounts, productNet == linesNet));
    }

    private async Task<List<ProductMarginRow>> ReadRowsAsync(ProductMarginFilter filter, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            LinesCte + """
            , consumed AS (
                SELECT c.source_reference_id AS order_item_id, c.stock_item_id,
                       c.quantity - COALESCE((
                           SELECT SUM(r.quantity) FROM inventory.stock_movements r
                           WHERE r.movement_type = 'Reversal' AND r.source_reference_id = c.stock_movement_id), 0) AS quantity
                FROM inventory.stock_movements c
                WHERE c.movement_type = 'Consumption' AND c.source_type = 'Order'
                  AND c.source_reference_id IN (SELECT order_item_id FROM lines)
            ),
            valued AS (
                SELECT l.order_item_id, u.quantity,
                       ac.unit_cost,
                       u.quantity * ac.unit_cost AS cost
                FROM lines l
                JOIN consumed u ON u.order_item_id = l.order_item_id AND u.quantity > 0
                LEFT JOIN LATERAL (
                    SELECT SUM(gri.accepted_quantity * gri.unit_price) / NULLIF(SUM(gri.accepted_quantity), 0) AS unit_cost
                    FROM purchasing.goods_receipt_items gri
                    JOIN purchasing.goods_receipts gr ON gr.receipt_id = gri.receipt_id
                    WHERE gri.stock_item_id = u.stock_item_id
                      AND gri.accepted_quantity > 0
                      AND (gr.received_at AT TIME ZONE 'Europe/Istanbul')::date <= l.business_date
                ) ac ON true
            ),
            line_cost AS (
                SELECT l.order_item_id,
                       COALESCE(SUM(v.cost) FILTER (WHERE v.unit_cost > 0), 0) AS cost,
                       (COUNT(v.order_item_id) = 0 OR COUNT(*) FILTER (WHERE v.unit_cost IS NULL OR v.unit_cost <= 0) > 0) AS unknown
                FROM lines l
                LEFT JOIN valued v ON v.order_item_id = l.order_item_id
                GROUP BY l.order_item_id
            )
            SELECT l.product_id,
                   (array_agg(l.product_name_snapshot ORDER BY l.business_date DESC))[1] AS name,
                   COALESCE(SUM(l.quantity) FILTER (WHERE l.line_type = 'Sale'), 0) AS sold_quantity,
                   COALESCE(SUM(l.quantity) FILTER (WHERE l.line_type = 'Complimentary'), 0) AS given_away_quantity,
                   COALESCE(SUM(l.net_amount) FILTER (WHERE l.line_type = 'Sale'), 0) AS net_revenue,
                   COALESCE(SUM(lc.cost), 0) AS cost,
                   COUNT(*) FILTER (WHERE lc.unknown) AS unknown_lines
            FROM lines l
            JOIN line_cost lc ON lc.order_item_id = l.order_item_id
            GROUP BY l.product_id
            ORDER BY net_revenue DESC, name
            LIMIT @limit;
            """);
        AddRange(command, filter);
        command.Parameters.AddWithValue("limit", MaxRows + 1);

        var rows = new List<ProductMarginRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var net = reader.GetDecimal(4);
            var cost = Math.Round(reader.GetDecimal(5), 2, MidpointRounding.AwayFromZero);
            var unknown = (int)reader.GetInt64(6);
            decimal? margin = unknown == 0 ? net - cost : null;
            decimal? percent = margin is not null && net > 0 ? Math.Round(margin.Value * 100m / net, 2, MidpointRounding.AwayFromZero) : null;
            rows.Add(new ProductMarginRow(
                reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), net, cost, unknown, margin, percent));
        }

        return rows;
    }

    private async Task<(decimal LinesNet, decimal BillDiscounts)> ReadControlTotalsAsync(
        ProductMarginFilter filter, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            LinesCte + """
            SELECT COALESCE((SELECT SUM(net_amount) FROM lines WHERE line_type = 'Sale'), 0),
                   COALESCE((SELECT SUM(b.discount_total) FROM billing.bills b
                             WHERE b.status = 'Paid'
                               AND (b.opened_at AT TIME ZONE 'Europe/Istanbul')::date BETWEEN @from AND @to), 0);
            """);
        AddRange(command, filter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private static void AddRange(NpgsqlCommand command, ProductMarginFilter filter)
    {
        command.Parameters.AddWithValue("from", filter.From);
        command.Parameters.AddWithValue("to", filter.To);
    }
}
