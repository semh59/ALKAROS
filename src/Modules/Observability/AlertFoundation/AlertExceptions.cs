namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// Base exception for domain errors in Alert Foundation (V1-ALT-001).
/// </summary>
public abstract class AlertException : Exception
{
    protected AlertException(string message) : base(message) { }
    protected AlertException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an alert is not found.
/// </summary>
public sealed class AlertNotFoundException : AlertException
{
    public AlertNotFoundException(Guid alertId)
        : base($"Alert with ID '{alertId}' was not found.")
    {
        AlertId = alertId;
    }

    public Guid AlertId { get; }
}

/// <summary>
/// Thrown when an alert transition is invalid based on current status.
/// </summary>
public sealed class InvalidAlertStateException : AlertException
{
    public InvalidAlertStateException(Guid alertId, AlertStatus currentStatus, string attemptedAction)
        : base($"Cannot perform '{attemptedAction}' on alert {alertId} because it is in '{currentStatus}' status.")
    {
        AlertId = alertId;
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }

    public Guid AlertId { get; }
    public AlertStatus CurrentStatus { get; }
    public string AttemptedAction { get; }
}

/// <summary>
/// Thrown when optimistic concurrency conflict occurs on alert row_version.
/// </summary>
public sealed class AlertConcurrencyException : AlertException
{
    public AlertConcurrencyException(Guid alertId, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict on alert {alertId}: expected row version {expectedVersion}, actual {actualVersion}.")
    {
        AlertId = alertId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public Guid AlertId { get; }
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }
}
