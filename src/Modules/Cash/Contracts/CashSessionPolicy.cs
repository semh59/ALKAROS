namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Domain policy engine enforcing single-open session, variance threshold, and lifecycle invariants (V1-CSH-001).
/// </summary>
public interface ICashSessionPolicy
{
    /// <summary>
    /// Validates whether a new cash session can be opened on a terminal.
    /// Enforces CSH-INV-01: Terminal must not have any other active session.
    /// </summary>
    void ValidateCanOpenSession(
        OpenCashSessionCommand command,
        IEnumerable<CashSessionSnapshot> existingTerminalSessions);

    /// <summary>
    /// Validates whether counting phase can be initiated for a session.
    /// </summary>
    void ValidateCanStartCount(CashSessionSnapshot session, Guid cashierUserId);

    /// <summary>
    /// Validates whether a physical cash count entry can be recorded.
    /// </summary>
    void ValidateCanRecordCount(CashSessionSnapshot session, decimal countedAmount);

    /// <summary>
    /// Validates whether a cash session can be closed and calculates variance.
    /// Requires supervisor override if variance exceeds the tolerance threshold.
    /// </summary>
    decimal ValidateCanCloseSession(
        CashSessionSnapshot session,
        decimal actualCash,
        decimal expectedCash,
        bool isSupervisorOverride,
        decimal varianceTolerance = 50.00m);

    /// <summary>
    /// Validates whether a closed cash session can be reconciled.
    /// </summary>
    void ValidateCanReconcile(CashSessionSnapshot session);
}

/// <summary>
/// Domain policy engine implementation for CashSession (V1-CSH-001).
/// </summary>
public sealed class CashSessionPolicy : ICashSessionPolicy
{
    public void ValidateCanOpenSession(
        OpenCashSessionCommand command,
        IEnumerable<CashSessionSnapshot> existingTerminalSessions)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(existingTerminalSessions);
        command.Validate();

        var activeExisting = existingTerminalSessions.FirstOrDefault(s => s.IsActive);
        if (activeExisting is not null)
        {
            throw new ActiveCashSessionExistsException(command.TerminalId, activeExisting.CashSessionId);
        }
    }

    public void ValidateCanStartCount(CashSessionSnapshot session, Guid cashierUserId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status != CashSessionStatus.Open)
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "StartCount");
        }
    }

    public void ValidateCanRecordCount(CashSessionSnapshot session, decimal countedAmount)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status is not (CashSessionStatus.Open or CashSessionStatus.Counting or CashSessionStatus.Closing))
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "RecordCashCount");
        }

        if (countedAmount < 0)
        {
            throw new NegativeCashAmountException(nameof(countedAmount), countedAmount);
        }
    }

    public decimal ValidateCanCloseSession(
        CashSessionSnapshot session,
        decimal actualCash,
        decimal expectedCash,
        bool isSupervisorOverride,
        decimal varianceTolerance = 50.00m)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status is not (CashSessionStatus.Open or CashSessionStatus.Counting or CashSessionStatus.Closing))
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "CloseSession");
        }

        if (actualCash < 0)
        {
            throw new NegativeCashAmountException(nameof(actualCash), actualCash);
        }

        var difference = actualCash - expectedCash;
        var absDifference = Math.Abs(difference);

        if (absDifference > varianceTolerance && !isSupervisorOverride)
        {
            throw new CashVarianceThresholdExceededException(session.CashSessionId, difference, varianceTolerance);
        }

        return difference;
    }

    public void ValidateCanReconcile(CashSessionSnapshot session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status != CashSessionStatus.Closed)
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "Reconcile");
        }
    }
}
