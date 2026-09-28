namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// V0-DOM-007: `direction` is never written by the application - it is
/// derived (by the database's own generated column, and mirrored here for
/// validation/display) from `transaction_type`, except `Adjustment`, whose
/// direction follows the sign of its own `amount`.
/// </summary>
public enum AccountTransactionDirection
{
    Debit,
    Credit,
}
