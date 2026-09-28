namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// V0-DAT-004's "AccountBalance" projection row (`customer_account.balances`,
/// migration 162) - a cached, derived value. `TransactionLedger.
/// AccountTransaction` rows are the sole authoritative source; this record
/// must always be exactly reproducible by summing them
/// (<see cref="IAccountBalanceProjection.RebuildAsync"/>).
/// </summary>
public sealed record CustomerAccountBalance(
    Guid CustomerId,
    decimal CurrentBalance,
    DateTimeOffset? LastTransactionAt,
    DateTimeOffset UpdatedAt);
