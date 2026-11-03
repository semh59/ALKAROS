namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Base exception for domain errors in Cash Module (V1-CSH-001).
/// </summary>
public abstract class CashSessionException : Exception
{
    protected CashSessionException(string message) : base(message) { }
    protected CashSessionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a requested cash session is not found.
/// </summary>
public sealed class CashSessionNotFoundException : CashSessionException
{
    public CashSessionNotFoundException(Guid sessionId)
        : base($"Cash session '{sessionId}' was not found.")
    {
        SessionId = sessionId;
    }

    public Guid SessionId { get; }
}

/// <summary>
/// Thrown when attempting to open a cash session on a terminal that already has an active session (CSH-INV-01).
/// </summary>
public sealed class ActiveCashSessionExistsException : CashSessionException
{
    public ActiveCashSessionExistsException(Guid terminalId, Guid existingSessionId)
        : base($"Terminal '{terminalId}' already has an active cash session '{existingSessionId}'. Terminal must have at most 1 active session.")
    {
        TerminalId = terminalId;
        ExistingSessionId = existingSessionId;
    }

    public Guid TerminalId { get; }
    public Guid ExistingSessionId { get; }
}

/// <summary>
/// Thrown when an attempted action is invalid for the current cash session lifecycle state.
/// </summary>
public sealed class InvalidCashSessionStateException : CashSessionException
{
    public InvalidCashSessionStateException(Guid sessionId, CashSessionStatus currentStatus, string attemptedAction)
        : base($"Cannot perform '{attemptedAction}' on cash session {sessionId} because it is in '{currentStatus}' status.")
    {
        SessionId = sessionId;
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }

    public Guid SessionId { get; }
    public CashSessionStatus CurrentStatus { get; }
    public string AttemptedAction { get; }
}

/// <summary>
/// Thrown when an opening balance or counted cash amount is negative.
/// </summary>
public sealed class NegativeCashAmountException : CashSessionException
{
    public NegativeCashAmountException(string parameterName, decimal amount)
        : base($"Amount '{amount}' for '{parameterName}' cannot be negative.")
    {
        ParameterName = parameterName;
        Amount = amount;
    }

    public string ParameterName { get; }
    public decimal Amount { get; }
}

/// <summary>
/// Thrown when closing variance exceeds tolerance and no supervisor override is provided.
/// </summary>
public sealed class CashVarianceThresholdExceededException : CashSessionException
{
    public CashVarianceThresholdExceededException(Guid sessionId, decimal difference, decimal threshold)
        : base($"Cash session {sessionId} variance of {difference:F2} exceeds tolerance threshold of {threshold:F2}. Supervisor override is required to close.")
    {
        SessionId = sessionId;
        Difference = difference;
        Threshold = threshold;
    }

    public Guid SessionId { get; }
    public decimal Difference { get; }
    public decimal Threshold { get; }
}

/// <summary>Thrown when someone other than the session's cashier (and not a manager) counts, closes or starts counting it.</summary>
public sealed class CashSessionOwnerRequiredException : CashSessionException
{
    public CashSessionOwnerRequiredException(Guid sessionId, Guid actorUserId)
        : base($"User {actorUserId} is neither the cashier of cash session {sessionId} nor a manager.")
    {
        SessionId = sessionId;
        ActorUserId = actorUserId;
    }

    public Guid SessionId { get; }
    public Guid ActorUserId { get; }
}

/// <summary>Thrown when a session is closed before any physical count was recorded for it.</summary>
public sealed class CashCountRequiredException(Guid sessionId)
    : CashSessionException($"Cash session {sessionId} has no recorded count; count the drawer before closing.")
{
    public Guid SessionId { get; } = sessionId;
}

/// <summary>Thrown when the amount a session is closed with differs from the latest recorded count.</summary>
public sealed class CashCountMismatchException(Guid sessionId, decimal recorded, decimal closingAmount)
    : CashSessionException($"Cash session {sessionId} was counted as {recorded:F2} but closing with {closingAmount:F2}.")
{
    public Guid SessionId { get; } = sessionId;
    public decimal Recorded { get; } = recorded;
    public decimal ClosingAmount { get; } = closingAmount;
}

/// <summary>Thrown when a closing difference has no explanation (or one longer than the limit).</summary>
public sealed class CashDifferenceExplanationRequiredException(Guid sessionId, decimal difference)
    : CashSessionException($"Cash session {sessionId} closes with a difference of {difference:F2} and needs an explanation of 1-{CashSessionPolicy.MaxExplanationLength} characters.")
{
    public Guid SessionId { get; } = sessionId;
    public decimal Difference { get; } = difference;
}

/// <summary>Thrown when the cashier or the closer of a session reconciles it while self reconciliation is not allowed.</summary>
public sealed class CashReconcileSelfNotAllowedException(Guid sessionId, Guid reconcilerUserId)
    : CashSessionException($"User {reconcilerUserId} ran or closed cash session {sessionId} and may not reconcile it.")
{
    public Guid SessionId { get; } = sessionId;
    public Guid ReconcilerUserId { get; } = reconcilerUserId;
}

/// <summary>Thrown when a session is closed while bills or payments are unsettled and the cashier did not acknowledge it.</summary>
public sealed class CashCloseUnsettledNotAcknowledgedException(Guid sessionId, int openBills, int unsettledPayments)
    : CashSessionException($"Cash session {sessionId} closes with {openBills} open bill(s) and {unsettledPayments} unsettled payment(s) that were not acknowledged.")
{
    public Guid SessionId { get; } = sessionId;
    public int OpenBills { get; } = openBills;
    public int UnsettledPayments { get; } = unsettledPayments;
}
