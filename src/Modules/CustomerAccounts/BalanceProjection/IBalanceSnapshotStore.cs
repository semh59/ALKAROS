namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// Persistence for dated balance snapshots (`customer_account.balance_snapshots`,
/// migration 162). A snapshot is the closing balance of one day, taken at most
/// once per customer per date (unique constraint) and never edited afterward -
/// a ledger row back-dated into an already snapshotted day does not change it.
/// </summary>
public interface IBalanceSnapshotStore
{
    /// <summary>
    /// Writes the closing balance of <paramref name="snapshotDate"/> for every customer with a ledger row before
    /// <paramref name="cutoff"/>, computed from the ledger itself (never from the live projection, which would freeze
    /// whatever moment the job happened to run). Idempotent: dates already snapshotted are left untouched.
    /// Returns how many snapshots were written.
    /// </summary>
    Task<int> TakeClosingSnapshotsAsync(DateOnly snapshotDate, DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    /// <summary>The dates from <paramref name="from"/> on that already have at least one snapshot; lets the job skip finished days.</summary>
    Task<IReadOnlySet<DateOnly>> GetSnapshotDatesAsync(DateOnly from, CancellationToken cancellationToken = default);

    /// <summary>Deletes the snapshots dated before <paramref name="before"/> and returns how many went; deleting twice is harmless.</summary>
    Task<int> DeleteOlderThanAsync(DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>The snapshots of every customer for the given dates, one query.</summary>
    Task<IReadOnlyList<BalanceSnapshot>> GetSnapshotsOnAsync(IReadOnlyCollection<DateOnly> dates, CancellationToken cancellationToken = default);
}
