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
