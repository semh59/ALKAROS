using ALKAROS.Billing.BillFoundation;
using System.Data;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Host.Experience.Orders.OrderCancellation;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders.OrderCleanup;

public enum HangingOrderOutcome
{
    Cleared,
    Changed,
    HasPayment,
    HasServedItems,
}

/// <param name="StockLines">Active lines the kitchen has not started: their stock goes back.</param>
/// <param name="WasteLines">Active lines the kitchen has started: waste.</param>
public sealed record HangingOrderV1(
    Guid OrderId, string OrderNumber, string Status, DateTimeOffset CreatedAt, string? TableNumber,
    int StockLines, int WasteLines, int ServedLines);

/// <summary>
/// Orders nobody is working on any more: an order opened without any product, and a dine-in order that stayed
/// live without a bill. Both are cancelled through the whole-order cancellation, one transaction per order, after
/// the candidate rule has been checked again under the order row lock.
/// </summary>
public sealed class OrderCleanupStore(NpgsqlDataSource dataSource, OrderCancellationStore cancellation)
{
    /// <summary>The actor that stands in for "the system" in audit records and order history.</summary>
    public static readonly Guid SystemActorId = new("00000000-0000-0000-0000-000000000802");

    public const string EmptyOrderReason = "Ürünsüz açık sipariş zaman aşımına uğradı.";

    private static readonly string EmptyDraft =
        $"""
        o.status = 'Draft' AND o.source IN ('Cashier', 'Waiter') AND o.updated_at < now() - @age
        AND NOT EXISTS (SELECT 1 FROM orders.order_items i WHERE i.order_id = o.order_id AND i.status IN ('Draft', 'Active'))
        AND NOT EXISTS (SELECT 1 FROM billing.bills b WHERE {BillOrderLink.Matches("b", "o.order_id")})
        """;

    private static readonly string Hanging =
        $"""
        o.status IN ('Submitted', 'Accepted', 'Preparing', 'Ready') AND o.source IN ('Cashier', 'Waiter')
        AND o.created_at < now() - @age
        AND NOT EXISTS (SELECT 1 FROM billing.bills b WHERE {BillOrderLink.Matches("b", "o.order_id")} AND b.status <> 'Cancelled')
        """;

    public async Task<IReadOnlyList<Guid>> FindEmptyDraftsAsync(TimeSpan idleFor, int limit, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT o.order_id FROM orders.orders o WHERE {EmptyDraft} ORDER BY o.updated_at LIMIT @limit;");
        command.Parameters.Add("age", NpgsqlDbType.Interval).Value = idleFor;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    /// <returns>False when the order is no longer an empty draft (a product was added, a bill opened, it was touched).</returns>
    public Task<bool> CancelEmptyDraftAsync(Guid orderId, TimeSpan idleFor, CancellationToken cancellationToken)
        => CancelIfAsync(orderId, EmptyDraft, idleFor, EmptyOrderReason, SystemActorId, "System", "empty-order-sweeper", cancellationToken);

    public async Task<IReadOnlyList<HangingOrderV1>> ListHangingAsync(TimeSpan olderThan, int limit, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
            SELECT o.order_id, o.order_number, o.status, o.created_at, t.table_number,
                   count(i.order_item_id) FILTER (WHERE i.status = 'Active' AND i.kitchen_state IN ('Sent', 'Held')),
                   count(i.order_item_id) FILTER (WHERE i.status = 'Active' AND i.kitchen_state IN ('Preparing', 'Ready')),
                   count(i.order_item_id) FILTER (WHERE i.status = 'Active' AND i.kitchen_state = 'Served')
            FROM orders.orders o
            LEFT JOIN orders.order_items i ON i.order_id = o.order_id
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            WHERE {Hanging}
            GROUP BY o.order_id, o.order_number, o.status, o.created_at, t.table_number
            ORDER BY o.created_at
            LIMIT @limit;
            """);
        command.Parameters.Add("age", NpgsqlDbType.Interval).Value = olderThan;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        var rows = new List<HangingOrderV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new HangingOrderV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), (int)reader.GetInt64(5), (int)reader.GetInt64(6), (int)reader.GetInt64(7)));
        return rows;
    }

    public async Task<HangingOrderOutcome> ClearHangingAsync(
        Guid orderId, TimeSpan olderThan, string reason, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            return await CancelIfAsync(orderId, Hanging, olderThan, reason, actorId, "User", correlationId, cancellationToken)
                ? HangingOrderOutcome.Cleared
                : HangingOrderOutcome.Changed;
        }
        catch (OrderCancelHasPaymentException) { return HangingOrderOutcome.HasPayment; }
        catch (OrderCancelHasServedItemsException) { return HangingOrderOutcome.HasServedItems; }
        catch (OrderConcurrencyException) { return HangingOrderOutcome.Changed; }
    }

    private async Task<bool> CancelIfAsync(
        Guid orderId, string candidateRule, TimeSpan age, string reason, Guid actorId, string actorType, string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Same order as every cancelling path: bill locks first, then the order row.
        await BillMoneyGuard.LockOrderBillsAsync(connection, transaction, orderId, [], cancellationToken);
        await using (var recheck = new NpgsqlCommand(
            $"SELECT o.order_id FROM orders.orders o WHERE o.order_id = @id AND {candidateRule} FOR UPDATE OF o;", connection, transaction))
        {
            recheck.Parameters.Add("id", NpgsqlDbType.Uuid).Value = orderId;
            recheck.Parameters.Add("age", NpgsqlDbType.Interval).Value = age;
            if (await recheck.ExecuteScalarAsync(cancellationToken) is null)
                return false;
        }

        await cancellation.CancelInTransactionAsync(
            orderId, reason, null, actorId, correlationId, connection, transaction, cancellationToken, actorType);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
