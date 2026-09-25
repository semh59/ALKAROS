using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// Refactor step 3/7 (docs/engineering/garson-refactor-plan.md, 2026-09-12):
/// the "hand a check off to the cashier" family, extracted out of the
/// former god-class <c>OrderManagementStore</c> — fully independent of the
/// table-draft flow (no shared state, no shared helper), the second most
/// independent group after <see cref="ShiftSummaryStore"/>.
/// </summary>
public sealed class CashierHandoffStore
{
    private readonly NpgsqlDataSource _dataSource;

    public CashierHandoffStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>
    /// V1-ORD-006: detaches an open check from its table — Semih's scenario
    /// (2026-09-10): the party has eaten, got up, and is queueing at the till
    /// while new guests are already waiting for the table.
    ///
    /// The check keeps its own identity and stays open for the cashier; the
    /// table stops pointing at it and goes to Cleaning, so the next party can
    /// be seated immediately. Both halves happen in one transaction: a check
    /// that left the table but was not released, or a table released while
    /// still pointing at the check, are each worse than not doing it at all.
    /// </summary>
    public async Task<SendCheckToCashierResultV1> SendCheckToCashierAsync(
        Guid tableId, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using (var detach = new NpgsqlCommand(
            """
            UPDATE table_mgmt.tables
            SET current_order_id = NULL,
                current_status = 'Cleaning',
                row_version = row_version + 1
            WHERE table_id = @table_id
              AND current_order_id = @order_id;
            """, connection, transaction))
        {
            detach.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            detach.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            if (await detach.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                // Either this check was already sent (a double tap, or the
                // other waiter got there first) or it never belonged to this
                // table. Both are "the world moved on", not a failure to
                // report as an error the waiter must act on.
                await transaction.RollbackAsync(cancellationToken);
                return new SendCheckToCashierResultV1(orderId, tableId, AlreadySent: true);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new SendCheckToCashierResultV1(orderId, tableId, AlreadySent: false);
    }

    /// <summary>
    /// V1-ORD-006: the cashier's queue — checks that left their table and are
    /// waiting to be settled. Keyed by check, not by table: the table has
    /// already been re-seated by the time the guest reaches the till.
    /// </summary>
    /// <summary>
    /// V1-RMD-281: takes a check that was sent to the till by mistake (or is being ordered on again) back to
    /// its table. Only while no money has moved: any allocation or unresolved payment on the check's bills
    /// closes this door, because the payment is already recorded against that bill. The table must not carry
    /// a newer check (the locked design never lets a pointer be silently replaced); the caller is told instead.
    /// The caller cancels the check's still-open bills through the Billing module (an Experience store never
    /// writes another area's schema), so the till has no ghost bill and a re-send builds a fresh one.
    /// </summary>
    public async Task<RecallCheckResultV1> RecallCheckAsync(
        Guid tableId, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        Guid? currentOrderId;
        await using (var lockTable = new NpgsqlCommand(
            "SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @table_id FOR UPDATE;", connection, transaction))
        {
            lockTable.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            await using var reader = await lockTable.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new CheckNotRecallableException("Masa bulunamadı.");
            currentOrderId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
        }

        if (currentOrderId == orderId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new RecallCheckResultV1(orderId, tableId, "AlreadyAttached");
        }
        if (currentOrderId is not null)
            throw new TableHasNewerCheckException(tableId);

        await using (var order = new NpgsqlCommand(
            "SELECT status, table_id FROM orders.orders WHERE order_id = @order_id FOR UPDATE;", connection, transaction))
        {
            order.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            await using var reader = await order.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)
                || reader.GetString(0) != "Submitted"
                || reader.IsDBNull(1) || reader.GetGuid(1) != tableId)
                throw new CheckNotRecallableException("Bu hesap kasa kuyruğunda değil.");
        }

        await using (var money = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM billing.bills b
                WHERE b.order_id = @order_id
                  AND (b.status = 'Paid'
                       OR EXISTS (SELECT 1 FROM payments.payment_allocations a WHERE a.bill_id = b.bill_id)
                       OR EXISTS (SELECT 1 FROM payments.payments p
                                  WHERE p.bill_id = b.bill_id AND p.status IN ('Pending', 'Unknown', 'ReconciliationRequired'))));
            """, connection, transaction))
        {
            money.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            if ((bool)(await money.ExecuteScalarAsync(cancellationToken))!)
                throw new CheckHasPaymentException(orderId);
        }

        await using (var attach = new NpgsqlCommand(
            """
            UPDATE table_mgmt.tables
            SET current_order_id = @order_id, current_status = 'Occupied', row_version = row_version + 1
            WHERE table_id = @table_id AND current_order_id IS NULL;
            """, connection, transaction))
        {
            attach.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            attach.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            if (await attach.ExecuteNonQueryAsync(cancellationToken) == 0)
                throw new TableHasNewerCheckException(tableId);
        }

        await transaction.CommitAsync(cancellationToken);
        return new RecallCheckResultV1(orderId, tableId, "Recalled");
    }

    public async Task<IReadOnlyList<PendingCheckSummaryV1>> GetChecksAwaitingPaymentAsync(
        CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT o.order_id,
                   o.order_number,
                   COALESCE(t.table_number, '—') AS table_number,
                   COUNT(i.order_item_id) FILTER (WHERE i.status = 'Active') AS item_count,
                   o.total,
                   o.created_at,
                   o.table_id,
                   ob.bill_id,
                   COALESCE(ob.paid_amount, 0),
                   (SELECT string_agg(preview.product_name_snapshot, ', ')
                    FROM (SELECT pi.product_name_snapshot
                          FROM orders.order_items pi
                          WHERE pi.order_id = o.order_id AND pi.status = 'Active'
                          ORDER BY pi.created_at, pi.order_item_id
                          LIMIT 3) preview) AS item_preview
            FROM orders.orders o
            LEFT JOIN orders.order_items i ON i.order_id = o.order_id
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            LEFT JOIN LATERAL (
                SELECT b.bill_id,
                       (SELECT SUM(pa.amount) FROM payments.payment_allocations pa WHERE pa.bill_id = b.bill_id) AS paid_amount
                FROM billing.bills b
                WHERE b.order_id = o.order_id AND b.status <> 'Cancelled'
                ORDER BY b.opened_at DESC
                LIMIT 1
            ) ob ON TRUE
            WHERE o.status = 'Submitted'
              AND NOT EXISTS (
                    SELECT 1 FROM table_mgmt.tables ct
                    WHERE ct.current_order_id = o.order_id)
              -- V1-RMD-279: a check whose bill is fully paid has left the till's queue. The order
              -- itself stays Submitted (the order lifecycle is a separate, larger gap), so the
              -- queue must look at the bill, not the order.
              AND NOT EXISTS (
                    SELECT 1 FROM billing.bills pb
                    WHERE pb.order_id = o.order_id AND pb.status = 'Paid')
            GROUP BY o.order_id, o.order_number, t.table_number, o.total, o.created_at, o.table_id, ob.bill_id, ob.paid_amount
            ORDER BY o.created_at;
            """);

        var results = new List<PendingCheckSummaryV1>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PendingCheckSummaryV1(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                (int)reader.GetInt64(3),
                reader.GetDecimal(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.GetDecimal(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(6) ? null : reader.GetGuid(6)));
        }
        return results;
    }
}

/// <summary>V1-RMD-281: the check is not (or no longer) waiting at the till for this table.</summary>
public sealed class CheckNotRecallableException : Exception
{
    public CheckNotRecallableException(string message) : base(message) { }
}

/// <summary>V1-RMD-281: money has already moved on the check; it cannot go back to the table.</summary>
public sealed class CheckHasPaymentException : Exception
{
    public Guid OrderId { get; }
    public CheckHasPaymentException(Guid orderId) : base($"Check {orderId} already has a payment.") { OrderId = orderId; }
}

/// <summary>V1-RMD-281: the table already carries a newer check; the caller must decide (send it first).</summary>
public sealed class TableHasNewerCheckException : Exception
{
    public Guid TableId { get; }
    public TableHasNewerCheckException(Guid tableId) : base($"Table {tableId} already has an open check.") { TableId = tableId; }
}
