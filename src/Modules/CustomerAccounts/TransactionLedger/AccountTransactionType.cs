namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// V0-DOM-007's own canonical `transaction_type` set
/// (`docs/domain/customer-credit-invoice-semantics.md`, PDF:III.18.3):
/// Charge (a Bill deferred to the account), Payment (receivable reduced),
/// Invoice (a periodic invoice referencing prior charges - never re-creates
/// them, V0-DOM-007 invariant 1), Credit/Debit (manual, not tied to a Bill
/// or Invoice), Adjustment (signed by its own amount, no fixed direction),
/// Refund (a collection reversal, always Credit-direction, never a reversal
/// of the original Payment row itself - V0-DOM-007 example 3).
/// </summary>
public enum AccountTransactionType
{
    Charge,
    Payment,
    Invoice,
    Credit,
    Debit,
    Adjustment,
    Refund,
}
