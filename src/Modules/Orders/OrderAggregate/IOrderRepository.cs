namespace ALKAROS.Orders.OrderAggregate;

/// <summary>
/// Persistence contract for the order aggregate. An order, its items,
/// modifiers and status history are persisted as one transaction. Writes are
/// guarded by the optimistic row version on the order row.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Loads an order with all items, modifiers and status history; returns
    /// null when no order with <paramref name="id"/> exists.
    /// </summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new order graph (order, items, modifiers, history) in a
    /// single transaction.
    /// </summary>
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes of an aggregate produced by a transition, guarded by
    /// the optimistic row version: the order row is updated only when its
    /// current version equals <paramref name="expectedRowVersion"/>. New items
    /// are inserted, existing items updated, and unpersisted history rows are
    /// appended. Returns the new order row version; throws
    /// <see cref="InvalidOperationException"/> when no row was updated
    /// (missing order or stale version).
    /// </summary>
    Task<long> SaveAsync(Order order, long expectedRowVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes of an aggregate within an existing connection and transaction for atomic operations.
    /// </summary>
    Task<long> SaveAsync(Order order, long expectedRowVersion, Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every non-terminal order from <paramref name="fromTableId"/> to
    /// <paramref name="toTableId"/> inside the caller's transaction and returns
    /// the number of rows changed. Table Management uses this for merge and
    /// transfer; the table association is Order's state to write, so it is
    /// exposed here rather than mutated with raw SQL (V0-ARC-001; v1-wave25
    /// boundary remediation).
    /// </summary>
    Task<int> ReparentActiveOrdersToTableAsync(
        Guid fromTableId,
        Guid toTableId,
        DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves one non-terminal order back to <paramref name="toTableId"/> only
    /// when it is currently on <paramref name="expectedFromTableId"/> (unmerge
    /// restore). Returns the number of rows changed (0 or 1).
    /// </summary>
    Task<int> ReparentOrderToTableAsync(
        Guid orderId,
        Guid expectedFromTableId,
        Guid toTableId,
        DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassigns every non-terminal order currently attributed to
    /// <paramref name="fromUserId"/> to <paramref name="toUserId"/> — the
    /// explicit hand-off operation (V1-RMD-111) behind
    /// orders.transfer-server[-any]. Returns the number of orders changed.
    /// </summary>
    Task<int> ReassignServingUserAsync(
        Guid fromUserId,
        Guid toUserId,
        CancellationToken cancellationToken = default);
}
