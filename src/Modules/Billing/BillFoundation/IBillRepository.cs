namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// Persistence contract for the Bill aggregate.
/// A Bill and its BillItems are persisted as one transactional boundary.
/// Concurrency is guarded by the optimistic row version on the bill row.
/// </summary>
public interface IBillRepository
{
    /// <summary>
    /// Loads a Bill with all its BillItems by bill ID. Returns null if not found.
    /// </summary>
    Task<Bill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a Bill with all its BillItems by unique bill number. Returns null if not found.
    /// </summary>
    Task<Bill?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all Bills associated with an origin order ID.
    /// </summary>
    Task<IReadOnlyList<Bill>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all Bills associated with a table ID.
    /// </summary>
    Task<IReadOnlyList<Bill>> GetByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new Bill and its BillItems in a single database transaction.
    /// </summary>
    Task AddAsync(Bill bill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists state changes or item modifications on an existing Bill aggregate.
    /// Guarded by optimistic concurrency: fails if current row version differs from <paramref name="expectedRowVersion"/>.
    /// Returns the incremented row version.
    /// </summary>
    Task<long> SaveAsync(Bill bill, long expectedRowVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an order item has already been billed in any active BillItem.
    /// </summary>
    Task<bool> IsOrderItemBilledAsync(Guid orderItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of given order item IDs that have already been billed.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetBilledOrderItemIdsAsync(IEnumerable<Guid> orderItemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every non-terminal bill from <paramref name="fromTableId"/> to
    /// <paramref name="toTableId"/> inside the caller's transaction and returns
    /// the number of rows changed. Table Management uses this for merge and
    /// transfer; the table association is Bill's state to write, so it is
    /// exposed here rather than mutated with raw SQL (V0-ARC-001).
    /// </summary>
    Task<int> ReparentActiveBillsToTableAsync(
        Guid fromTableId,
        Guid toTableId,
        DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves one non-terminal bill back to <paramref name="toTableId"/> only
    /// when it is currently on <paramref name="expectedFromTableId"/> (unmerge
    /// restore). Returns the number of rows changed (0 or 1).
    /// </summary>
    Task<int> ReparentBillToTableAsync(
        Guid billId,
        Guid expectedFromTableId,
        Guid toTableId,
        DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-414: cancels every still-open (not Paid, not Cancelled) bill of <paramref name="orderId"/> inside the
    /// caller's transaction, so the caller's own checks (a check recalled from the till has no money on it) and
    /// the cancellation commit together. Returns the number of bills cancelled.
    /// </summary>
    Task<int> CancelActiveBillsForOrderAsync(
        Guid orderId,
        DateTimeOffset timestamp,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
