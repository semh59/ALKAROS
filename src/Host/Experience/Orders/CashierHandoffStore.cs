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
                   o.created_at
            FROM orders.orders o
            LEFT JOIN orders.order_items i ON i.order_id = o.order_id
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            WHERE o.status = 'Submitted'
              AND NOT EXISTS (
                    SELECT 1 FROM table_mgmt.tables ct
                    WHERE ct.current_order_id = o.order_id)
            GROUP BY o.order_id, o.order_number, t.table_number, o.total, o.created_at
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
                reader.GetFieldValue<DateTimeOffset>(5)));
        }
        return results;
    }
}
