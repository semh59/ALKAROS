namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// Read access to the cached `customer_account.balances` projection, plus
/// the full-rebuild path V0-DAT-004 requires every projection to have. This
/// task never publishes new account transactions itself (Out of scope) -
/// `customer_account.balances` is kept current by the database's own
/// `apply_transaction_to_balance` trigger (migration 162), atomic with the
/// insert by construction, not by any code in this interface.
/// </summary>
public interface IAccountBalanceProjection
{
    /// <summary>Returns null if the customer has never had a transaction recorded (the trigger never created a row for them).</summary>
    Task<CustomerAccountBalance?> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes the balance purely by summing every
    /// `TransactionLedger.AccountTransaction` for this customer and
    /// REPLACES the cached row entirely - proves the projection is a real
    /// derived value, not an independent source of truth (V0-DAT-004's own
    /// "Full rebuild from transactions" rule).
    /// </summary>
    Task<CustomerAccountBalance> RebuildAsync(Guid customerId, CancellationToken cancellationToken = default);
}
