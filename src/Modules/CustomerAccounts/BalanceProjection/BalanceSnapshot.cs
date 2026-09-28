namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// A dated, immutable point-in-time balance record
/// (`customer_account.balance_snapshots`, migration 162) - the basis a
/// later aging/collections report can build on without re-deriving history
/// from the full transaction ledger every time.
/// </summary>
public sealed record BalanceSnapshot(
    Guid Id,
    Guid CustomerId,
    DateOnly SnapshotDate,
    decimal Balance,
    DateTimeOffset CreatedAt);
