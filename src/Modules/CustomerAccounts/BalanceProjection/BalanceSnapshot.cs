namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// A dated, immutable closing balance (`customer_account.balance_snapshots`,
/// migration 162); the customer aging report shows it as the balance 30/60/90 days ago.
/// </summary>
public sealed record BalanceSnapshot(
    Guid Id,
    Guid CustomerId,
    DateOnly SnapshotDate,
    decimal Balance,
    DateTimeOffset CreatedAt);
