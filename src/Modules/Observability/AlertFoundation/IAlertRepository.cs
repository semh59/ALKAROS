namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// Repository interface for alert lifecycle, deduplication, and audit event management (V1-ALT-001, PDF:III.28).
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// Retrieves an alert by its ID.
    /// </summary>
    Task<AlertRecord?> GetByIdAsync(
        Guid alertId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active alerts (Open, Acknowledged, Escalated).
    /// </summary>
    Task<IReadOnlyList<AlertRecord>> GetActiveAlertsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves alerts associated with a specific source reference.
    /// </summary>
    Task<IReadOnlyList<AlertRecord>> GetBySourceReferenceAsync(
        string sourceReferenceType,
        Guid sourceReferenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the append-only event trail for an alert.
    /// </summary>
    Task<IReadOnlyList<AlertEventRecord>> GetEventsAsync(
        Guid alertId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises an alert: if an active alert matches deduplication criteria, records a Deduplicated event; otherwise creates a new Open alert.
    /// </summary>
    Task<AlertRaiseResult> RaiseAlertAsync(
        RaiseAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions an alert to Acknowledged status with user attribution.
    /// </summary>
    Task<AlertRecord> AcknowledgeAlertAsync(
        AcknowledgeAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions an alert to Escalated status.
    /// </summary>
    Task<AlertRecord> EscalateAlertAsync(
        EscalateAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions an alert to Suppressed status.
    /// </summary>
    Task<AlertRecord> SuppressAlertAsync(
        SuppressAlertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions an alert to Resolved status with resolution reason and actor attribution.
    /// </summary>
    Task<AlertRecord> ResolveAlertAsync(
        ResolveAlertRequest request,
        CancellationToken cancellationToken = default);
}
