using ALKAROS.Billing.BillFoundation;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Billing;

public sealed record RecentSaleV1(Guid BillId, string? TableNumber, DateTimeOffset ClosedAt, decimal Amount, IReadOnlyList<string> Methods);

/// <summary>The paid bills the caller closed in their own open drawer session; empty when there is no such session.</summary>
public sealed record RecentSalesV1(bool HasOpenSession, IReadOnlyList<RecentSaleV1> Sales);

public sealed class RecentSalesStore(NpgsqlDataSource dataSource)
{
    public const int Limit = 20;

    public async Task<RecentSalesV1> ListAsync(Guid cashierUserId, Guid terminalId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
            WITH own_session AS (
                SELECT opened_at FROM cash.cash_sessions
                WHERE terminal_id = @terminal AND cashier_user_id = @cashier AND status IN ('Open', 'Counting', 'Closing')
                LIMIT 1)
            SELECT b.bill_id,
                   COALESCE(bt.table_number, (SELECT ot.table_number FROM orders.orders o
                                              JOIN table_mgmt.tables ot ON ot.table_id = o.table_id
                                              WHERE {BillOrderLink.Matches("b", "o.order_id")}
                                              ORDER BY o.created_at LIMIT 1)),
                   b.closed_at,
                   COALESCE((SELECT SUM(a.amount) FROM payments.payment_allocations a WHERE a.bill_id = b.bill_id), 0)
                     - COALESCE((SELECT SUM(r.amount) FROM payments.payment_reversals r
                                 JOIN payments.payments rp ON rp.payment_id = r.payment_id
                                 WHERE rp.bill_id = b.bill_id), 0),
                   ARRAY(SELECT DISTINCT {PaymentMethodLookup.Sql("p.payment_id")} AS method
                         FROM payments.payments p
                         WHERE p.bill_id = b.bill_id AND p.status IN ('Approved', 'PartiallyRefunded')
                         ORDER BY 1)
            FROM own_session s
            JOIN billing.bills b ON b.status = 'Paid' AND b.closed_at >= s.opened_at
                                AND b.terminal_id = @terminal
            LEFT JOIN table_mgmt.tables bt ON bt.table_id = b.table_id
            ORDER BY b.closed_at DESC, b.bill_id
            LIMIT {Limit};
            """);
        command.Parameters.Add("terminal", NpgsqlDbType.Uuid).Value = terminalId;
        command.Parameters.Add("cashier", NpgsqlDbType.Uuid).Value = cashierUserId;

        var sales = new List<RecentSaleV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sales.Add(new RecentSaleV1(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetDecimal(3),
                reader.GetFieldValue<string[]>(4)));
        }

        if (sales.Count > 0)
            return new RecentSalesV1(true, sales);

        await reader.CloseAsync();
        await using var open = dataSource.CreateCommand(
            """
            SELECT EXISTS (SELECT 1 FROM cash.cash_sessions
                           WHERE terminal_id = @terminal AND cashier_user_id = @cashier AND status IN ('Open', 'Counting', 'Closing'));
            """);
        open.Parameters.Add("terminal", NpgsqlDbType.Uuid).Value = terminalId;
        open.Parameters.Add("cashier", NpgsqlDbType.Uuid).Value = cashierUserId;
        return new RecentSalesV1((bool)(await open.ExecuteScalarAsync(cancellationToken))!, sales);
    }
}
