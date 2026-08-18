namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Immutable domain state snapshot for a cash register session (V1-CSH-001, PDF:III.9.1).
/// </summary>
public sealed record CashSessionSnapshot(
    Guid CashSessionId,
    Guid CashierUserId,
    Guid TerminalId,
    CashSessionStatus Status,
    decimal OpeningBalance,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal Difference,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    long RowVersion)
{
    /// <summary>
    /// Returns true if the session is currently in an active, unclosed state.
    /// </summary>
    public bool IsActive => Status is CashSessionStatus.Open or CashSessionStatus.Counting or CashSessionStatus.Closing;
}
