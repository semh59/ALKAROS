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
}