namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// V0-DOM-007's balance formula, restated as a direction rule:
/// <c>receivable_balance = opening + Charge + Invoice + Debit + Adjustment(signed)
/// - Payment - Credit - Refund</c>. Charge/Invoice/Debit are the Debit-direction
/// (balance-increasing) types; Payment/Credit/Refund are Credit-direction
/// (balance-reducing); Adjustment alone has no fixed direction. This class
/// mirrors, but does not replace, the database's own generated `direction`
/// column (`docs/domain/customer-credit-invoice-semantics.md`: "direction is
/// never written by the application") - it exists so the application layer
/// can validate a transaction's declared direction BEFORE it ever reaches
/// the database, and so tests can assert the rule directly.
/// </summary>
public static class AccountTransactionDirectionRules
{
    private static readonly Dictionary<AccountTransactionType, AccountTransactionDirection> FixedDirections = new()
    {
        [AccountTransactionType.Charge] = AccountTransactionDirection.Debit,
        [AccountTransactionType.Invoice] = AccountTransactionDirection.Debit,
        [AccountTransactionType.Debit] = AccountTransactionDirection.Debit,
        [AccountTransactionType.Payment] = AccountTransactionDirection.Credit,
        [AccountTransactionType.Credit] = AccountTransactionDirection.Credit,
        [AccountTransactionType.Refund] = AccountTransactionDirection.Credit,
    };

    /// <summary>False only for <see cref="AccountTransactionType.Adjustment"/>.</summary>
    public static bool HasFixedDirection(AccountTransactionType type) => FixedDirections.ContainsKey(type);

    public static AccountTransactionDirection FixedDirectionFor(AccountTransactionType type) =>
        FixedDirections.TryGetValue(type, out var direction)
            ? direction
            : throw new ArgumentException($"{type} has no fixed direction; derive it from the signed amount instead.", nameof(type));

    /// <summary>Adjustment only: positive amount increases the receivable (Debit), negative reduces it (Credit).</summary>
    public static AccountTransactionDirection DirectionForAdjustment(decimal signedAmount) =>
        signedAmount >= 0 ? AccountTransactionDirection.Debit : AccountTransactionDirection.Credit;

    /// <summary>The direction a transaction of this type/amount must carry - the single source both the domain layer and tests use.</summary>
    public static AccountTransactionDirection DirectionFor(AccountTransactionType type, decimal amount) =>
        HasFixedDirection(type) ? FixedDirectionFor(type) : DirectionForAdjustment(amount);
}
