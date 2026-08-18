namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Canonical lifecycle states of a cash session (V1-CSH-001, PDF:II.5.9, PDF:III.9.1).
/// </summary>
public enum CashSessionStatus
{
    Open,
    Counting,
    Closing,
    Closed,
    Reconciled
}

/// <summary>
/// Canonical transaction types in the cash transaction ledger (V1-CSH-001, PDF:III.9.2).
/// </summary>
public enum CashTransactionType
{
    Opening,
    Sale,
    CashIn,
    CashOut,
    Refund,
    CountAdjustment,
    ClosingDifference
}
