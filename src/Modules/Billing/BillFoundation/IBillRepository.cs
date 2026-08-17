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
}
