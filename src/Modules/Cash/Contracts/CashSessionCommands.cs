namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Command to open a new cash session on a terminal (V1-CSH-001).
/// </summary>
public sealed record OpenCashSessionCommand(
    Guid CashSessionId,
    Guid CashierUserId,
    Guid TerminalId,
    decimal OpeningBalance)
{
    public void Validate()
    {
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session ID cannot be empty.", nameof(CashSessionId));

        if (CashierUserId == Guid.Empty)
            throw new ArgumentException("Cashier user ID cannot be empty.", nameof(CashierUserId));

        if (TerminalId == Guid.Empty)
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(TerminalId));

        if (OpeningBalance < 0)
            throw new NegativeCashAmountException(nameof(OpeningBalance), OpeningBalance);
    }
}

/// <summary>
/// Command to start the physical cash counting phase of an open session (V1-CSH-001).
/// </summary>
public sealed record StartCashCountCommand(
    Guid CashSessionId,
    Guid CashierUserId)
{
    public void Validate()
    {
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session ID cannot be empty.", nameof(CashSessionId));

        if (CashierUserId == Guid.Empty)
            throw new ArgumentException("Cashier user ID cannot be empty.", nameof(CashierUserId));
    }
}

/// <summary>
/// Command to record a physical cash count entry (V1-CSH-001, PDF:III.9.3).
/// </summary>
public sealed record RecordCashCountCommand(
    Guid CashSessionId,
    decimal CountedAmount,
    Guid CountedBy,
    string? Notes = null)
{
    public void Validate()
    {
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session ID cannot be empty.", nameof(CashSessionId));

        if (CountedBy == Guid.Empty)
            throw new ArgumentException("CountedBy user ID cannot be empty.", nameof(CountedBy));

        if (CountedAmount < 0)
            throw new NegativeCashAmountException(nameof(CountedAmount), CountedAmount);
    }
}

/// <summary>
/// Command to close a cash session and finalize variance (V1-CSH-001).
/// </summary>
public sealed record CloseCashSessionCommand(
    Guid CashSessionId,
    decimal ActualCash,
    Guid ClosedBy,
    bool IsSupervisorOverride = false,
    string? OverrideReason = null)
{
    public void Validate()
    {
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session ID cannot be empty.", nameof(CashSessionId));

        if (ClosedBy == Guid.Empty)
            throw new ArgumentException("ClosedBy user ID cannot be empty.", nameof(ClosedBy));

        if (ActualCash < 0)
            throw new NegativeCashAmountException(nameof(ActualCash), ActualCash);

        if (IsSupervisorOverride && string.IsNullOrWhiteSpace(OverrideReason))
            throw new ArgumentException("Override reason is required when performing supervisor override.", nameof(OverrideReason));
    }
}

/// <summary>
/// Command to reconcile a closed cash session during daily finance closure (V1-CSH-001).
/// </summary>
public sealed record ReconcileCashSessionCommand(
    Guid CashSessionId,
    Guid ReconciledBy,
    string? Notes = null)
{
    public void Validate()
    {
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session ID cannot be empty.", nameof(CashSessionId));

        if (ReconciledBy == Guid.Empty)
            throw new ArgumentException("ReconciledBy user ID cannot be empty.", nameof(ReconciledBy));
    }
}
