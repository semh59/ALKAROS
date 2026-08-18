namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// Command request to raise an alert (new creation or deduplicated event) (V1-ALT-001).
/// </summary>
public sealed record RaiseAlertRequest(
    string AlertType,
    AlertSeverity Severity,
    string Title,
    string Message,
    string? DeduplicationKey = null,
    string? SourceReferenceType = null,
    Guid? SourceReferenceId = null,
    Guid? ActorId = null,
    string? PayloadJson = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AlertType))
            throw new ArgumentException("Alert type cannot be null, empty, or whitespace.", nameof(AlertType));

        if (string.IsNullOrWhiteSpace(Title))
            throw new ArgumentException("Title cannot be null, empty, or whitespace.", nameof(Title));

        if (string.IsNullOrWhiteSpace(Message))
            throw new ArgumentException("Message cannot be null, empty, or whitespace.", nameof(Message));
    }
}

/// <summary>
/// Command request to acknowledge an active alert (V1-ALT-001).
/// </summary>
public sealed record AcknowledgeAlertRequest(
    Guid AlertId,
    long ExpectedRowVersion,
    Guid AcknowledgedBy,
    string? Reason = null)
{
    public void Validate()
    {
        if (AlertId == Guid.Empty)
            throw new ArgumentException("Alert ID cannot be empty.", nameof(AlertId));

        if (AcknowledgedBy == Guid.Empty)
            throw new ArgumentException("AcknowledgedBy cannot be empty.", nameof(AcknowledgedBy));

        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), ExpectedRowVersion, "Expected row version must be positive.");
    }
}

/// <summary>
/// Command request to escalate an active alert (V1-ALT-001).
/// </summary>
public sealed record EscalateAlertRequest(
    Guid AlertId,
    long ExpectedRowVersion,
    Guid? EscalatedBy = null,
    string? Reason = null)
{
    public void Validate()
    {
        if (AlertId == Guid.Empty)
            throw new ArgumentException("Alert ID cannot be empty.", nameof(AlertId));

        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), ExpectedRowVersion, "Expected row version must be positive.");
    }
}

/// <summary>
/// Command request to suppress an active alert (V1-ALT-001).
/// </summary>
public sealed record SuppressAlertRequest(
    Guid AlertId,
    long ExpectedRowVersion,
    Guid? SuppressedBy = null,
    string? Reason = null)
{
    public void Validate()
    {
        if (AlertId == Guid.Empty)
            throw new ArgumentException("Alert ID cannot be empty.", nameof(AlertId));

        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), ExpectedRowVersion, "Expected row version must be positive.");
    }
}

/// <summary>
/// Command request to resolve an alert (V1-ALT-001).
/// </summary>
public sealed record ResolveAlertRequest(
    Guid AlertId,
    long ExpectedRowVersion,
    Guid ResolvedBy,
    string ResolutionReason)
{
    public void Validate()
    {
        if (AlertId == Guid.Empty)
            throw new ArgumentException("Alert ID cannot be empty.", nameof(AlertId));

        if (ResolvedBy == Guid.Empty)
            throw new ArgumentException("ResolvedBy cannot be empty.", nameof(ResolvedBy));

        if (string.IsNullOrWhiteSpace(ResolutionReason))
            throw new ArgumentException("Resolution reason cannot be null, empty, or whitespace.", nameof(ResolutionReason));

        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), ExpectedRowVersion, "Expected row version must be positive.");
    }
}
