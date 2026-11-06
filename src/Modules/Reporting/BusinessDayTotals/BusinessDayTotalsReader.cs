using System.Data.Common;
using System.Text.Json;
using Npgsql;

namespace ALKAROS.Reporting.BusinessDayTotals;

/// <summary>
/// Every figure a business-day close stores or warns about, read from what was recorded rather than taken from the
/// caller. One call is one SQL statement, so the figures of a close are one consistent snapshot; inside a close the
/// caller passes its own connection and transaction so the read, the checks and the write share it.
/// </summary>
public interface IBusinessDayTotalsReader
{
    Task<BusinessDayFigures> ReadAsync(DateOnly businessDate, CancellationToken cancellationToken = default);

    Task<BusinessDayFigures> ReadAsync(
        DateOnly businessDate, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken = default);
}

/// <summary>What the business day recorded, as the server counted it.</summary>
/// <param name="Revenue">Approved amount of every payment approved inside the window, less the refunds paid out in
/// it, less the tips (a tip is a separate line, not revenue). Refunded money is netted, so the figure is net revenue.</param>
/// <param name="Tips">Tip adjustments of the bills closed (<c>Paid</c>) inside the window; a bill closed by a refund
/// (<c>Cancelled</c>) carries no tip here, so a refund never takes the same tip out twice.</param>
/// <param name="OrderCount">Orders submitted inside the window, excluding drafts, rejected and cancelled orders, orders
/// closed by a full refund and platform (online) orders, which carry no payment here and are reported per channel.</param>
/// <param name="CancelledItems">Item voids written inside the window (<c>Order.ItemVoided</c>, <c>Order.SentItemVoided</c>).</param>
/// <param name="PrintFailures">Print jobs that recorded a failure inside the window, even when printed later.</param>
public sealed record BusinessDayFigures(
    decimal Revenue,
    decimal Tips,
    int OrderCount,
    int CancelledItems,
    int PrintFailures,
    OpenItems OpenItems,
    IReadOnlyList<WaiterFigure> Waiters,
    IReadOnlyList<PrintStationFigure> PrintStations,
    IReadOnlyList<TableFigure> Tables,
    CashReconciliationFigure Cash);

/// <summary>What is still open right now; business-wide, since sessions, bills and payments are not bound to a window.</summary>
public sealed record OpenItems(int CashSessions, int Bills, int UnsettledPayments)
{
    public bool Any => CashSessions > 0 || Bills > 0 || UnsettledPayments > 0;
}

public sealed record WaiterFigure(Guid WaiterUserId, int Orders, decimal Sales, int Cancellations, decimal Discounts);

/// <param name="Total">Jobs created inside the window.</param>
/// <param name="Failed">Jobs that recorded a failure inside the window.</param>
/// <param name="Recovered">Of the failed ones, those that were printed afterwards.</param>
public sealed record PrintStationFigure(string Station, int Total, int Failed, int Recovered);

/// <param name="TableNumber">The number the table carried when the day was closed; null when it was not known (a day closed before numbers were stored, or a table that no longer exists).</param>
public sealed record TableFigure(Guid TableId, string? TableNumber, int Orders, decimal Sales);

/// <summary>Sessions closed inside the window.</summary>
public sealed record CashReconciliationFigure(
    int SessionsClosed, decimal Expected, decimal Counted, decimal Difference, int ManagerApprovedCloses, int SelfReconciled);

/// <summary>
/// The business-day window is <see cref="BusinessDayWindow"/>. Revenue follows the payment settlement report's mix:
/// payments whose status is Approved (or later refunded), summed by approved amount.
/// </summary>
public sealed class PostgresBusinessDayTotalsReader : IBusinessDayTotalsReader
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private const string Sql =
        """
        WITH valid_orders AS (
            SELECT o.order_id, o.serving_user_id, o.table_id, o.total, o.discount_total
            FROM orders.orders o
            WHERE o.submitted_at >= $1 AND o.submitted_at < $2
              AND o.status NOT IN ('Draft', 'Rejected', 'Cancelled')
              AND o.source <> 'Online'
              AND NOT (
               EXISTS (SELECT 1 FROM billing.bills rb
                       WHERE (rb.order_id = o.order_id OR EXISTS (SELECT 1 FROM billing.bill_items rbi
                                          JOIN orders.order_items rbo ON rbo.order_item_id = rbi.order_item_id
                                          WHERE rbi.bill_id = rb.bill_id AND rbo.order_id = o.order_id)) AND rb.status = 'Cancelled'
                         AND EXISTS (SELECT 1 FROM payments.payments rp
                                     WHERE rp.bill_id = rb.bill_id AND rp.status = 'Refunded'))
               AND NOT EXISTS (SELECT 1 FROM billing.bills pb
                               WHERE (pb.order_id = o.order_id OR EXISTS (SELECT 1 FROM billing.bill_items pbi
                                          JOIN orders.order_items pbo ON pbo.order_item_id = pbi.order_item_id
                                          WHERE pbi.bill_id = pb.bill_id AND pbo.order_id = o.order_id)) AND pb.status = 'Paid'))),
        voids AS (
            SELECT o.serving_user_id
            FROM audit.audit_events a
            JOIN orders.orders o ON o.order_id = a.aggregate_id
            WHERE a.aggregate_type = 'Order' AND a.event_name IN ('Order.ItemVoided', 'Order.SentItemVoided')
              AND a.occurred_at >= $1 AND a.occurred_at < $2),
        tips AS (
            SELECT COALESCE(SUM(a.amount), 0) AS amount
            FROM billing.bill_adjustments a
            JOIN billing.bills b ON b.bill_id = a.bill_id
            WHERE a.adjustment_type = 'Tip' AND b.status = 'Paid' AND b.closed_at >= $1 AND b.closed_at < $2)
        SELECT
            (SELECT COALESCE(SUM(approved_amount), 0) FROM payments.payments
              WHERE status IN ('Approved', 'Refunded', 'PartiallyRefunded') AND approved_at >= $1 AND approved_at < $2)
            - (SELECT COALESCE(SUM(amount), 0) FROM payments.payment_reversals WHERE occurred_at >= $1 AND occurred_at < $2)
            - (SELECT amount FROM tips),
            (SELECT amount FROM tips),
            (SELECT COUNT(*) FROM valid_orders),
            (SELECT COUNT(*) FROM audit.audit_events
              WHERE aggregate_type = 'Order' AND event_name IN ('Order.ItemVoided', 'Order.SentItemVoided')
                AND occurred_at >= $1 AND occurred_at < $2),
            (SELECT COUNT(*) FROM kitchen.print_jobs WHERE failed_at >= $1 AND failed_at < $2),
            (SELECT COUNT(*) FROM cash.cash_sessions WHERE status IN ('Open', 'Counting', 'Closing')),
            (SELECT COUNT(*) FROM billing.bills WHERE status NOT IN ('Paid', 'Cancelled')),
            (SELECT COUNT(*) FROM payments.payments WHERE status IN ('Pending', 'Unknown', 'ReconciliationRequired')),
            (SELECT COALESCE(jsonb_agg(w ORDER BY w."waiterUserId"), '[]'::jsonb)::text FROM (
                SELECT COALESCE(vo.serving_user_id, vd.serving_user_id) AS "waiterUserId",
                       COALESCE(vo.orders, 0) AS orders, COALESCE(vo.sales, 0) AS sales,
                       COALESCE(vd.cancellations, 0) AS cancellations, COALESCE(vo.discounts, 0) AS discounts
                FROM (SELECT serving_user_id, COUNT(*) AS orders, SUM(total) AS sales, SUM(discount_total) AS discounts
                      FROM valid_orders WHERE serving_user_id IS NOT NULL GROUP BY serving_user_id) vo
                FULL OUTER JOIN (SELECT serving_user_id, COUNT(*) AS cancellations
                                 FROM voids WHERE serving_user_id IS NOT NULL GROUP BY serving_user_id) vd
                  ON vd.serving_user_id = vo.serving_user_id) w),
            (SELECT COALESCE(jsonb_agg(p ORDER BY p.station), '[]'::jsonb)::text FROM (
                SELECT pr.station_id AS station,
                       COUNT(*) FILTER (WHERE j.created_at >= $1 AND j.created_at < $2) AS total,
                       COUNT(*) FILTER (WHERE j.failed_at >= $1 AND j.failed_at < $2) AS failed,
                       COUNT(*) FILTER (WHERE j.failed_at >= $1 AND j.failed_at < $2 AND j.status = 'Printed') AS recovered
                FROM kitchen.print_jobs j JOIN kitchen.printers pr ON pr.id = j.printer_id
                WHERE (j.created_at >= $1 AND j.created_at < $2) OR (j.failed_at >= $1 AND j.failed_at < $2)
                GROUP BY pr.station_id) p),
            (SELECT COALESCE(jsonb_agg(t ORDER BY t."tableId"), '[]'::jsonb)::text FROM (
                SELECT vo.table_id AS "tableId",
                       (SELECT tt.table_number FROM table_mgmt.tables tt WHERE tt.table_id = vo.table_id) AS "tableNumber",
                       COUNT(*) AS orders, COALESCE(SUM(vo.total), 0) AS sales
                FROM valid_orders vo WHERE vo.table_id IS NOT NULL GROUP BY vo.table_id) t),
            (SELECT jsonb_build_object(
                'sessionsClosed', COUNT(*), 'expected', COALESCE(SUM(expected_cash), 0), 'counted', COALESCE(SUM(actual_cash), 0),
                'difference', COALESCE(SUM(difference), 0),
                'managerApprovedCloses', COUNT(*) FILTER (WHERE is_supervisor_override),
                'selfReconciled', COUNT(*) FILTER (WHERE reconciled_self_approved))::text
             FROM cash.cash_sessions WHERE closed_at >= $1 AND closed_at < $2);
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBusinessDayTotalsReader(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<BusinessDayFigures> ReadAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAsync(businessDate, connection, null, cancellationToken);
    }

    public async Task<BusinessDayFigures> ReadAsync(
        DateOnly businessDate, DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var (start, end) = BusinessDayWindow.Of(businessDate);

        await using var command = new NpgsqlCommand(Sql, (NpgsqlConnection)connection, (NpgsqlTransaction?)transaction);
        command.Parameters.AddWithValue(start.UtcDateTime);
        command.Parameters.AddWithValue(end.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new BusinessDayFigures(
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            checked((int)reader.GetInt64(2)),
            checked((int)reader.GetInt64(3)),
            checked((int)reader.GetInt64(4)),
            new OpenItems(checked((int)reader.GetInt64(5)), checked((int)reader.GetInt64(6)), checked((int)reader.GetInt64(7))),
            Parse<WaiterFigure>(reader.GetString(8)),
            Parse<PrintStationFigure>(reader.GetString(9)),
            Parse<TableFigure>(reader.GetString(10)),
            JsonSerializer.Deserialize<CashReconciliationFigure>(reader.GetString(11), Json)!);
    }

    private static List<T> Parse<T>(string json) => JsonSerializer.Deserialize<List<T>>(json, Json)!;
}
