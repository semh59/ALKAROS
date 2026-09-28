namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// Persistence for dated balance snapshots (`customer_account.balance_snapshots`,
/// migration 162). Snapshot uniqueness: a unique (customer_id, snapshot_date)
/// constraint backs every method here - a snapshot is a point-in-time fact,
/// taken at most once per customer per date, and never edited afterward.
/// </summary>
public interface IBalanceSnapshotStore
{
    /// <summary>
    /// Idempotent: retrying with the same (customerId, snapshotDate) returns
    /// the snapshot already taken that day rather than creating a duplicate
    /// or silently overwriting it with a possibly-different balance.
    /// </summary>
    Task<BalanceSnapshot> TakeSnapshotAsync(Guid customerId, DateOnly snapshotDate, decimal balance, CancellationToken cancellationToken = default);

    Task<BalanceSnapshot?> GetSnapshotAsync(Guid customerId, DateOnly snapshotDate, CancellationToken cancellationToken = default);

    /// <summary>All snapshots for a customer, oldest first, bounded by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<BalanceSnapshot>> GetSnapshotsAsync(Guid customerId, int limit = 1000, CancellationToken cancellationToken = default);
}
