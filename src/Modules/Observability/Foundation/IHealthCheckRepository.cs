namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Repository interface for persisting and querying health check results (V1-OBS-001, PDF:III.28.1).
/// </summary>
public interface IHealthCheckRepository
{
    /// <summary>
    /// Records a health check execution result with approved retention policy validation.
    /// </summary>
    Task<HealthCheckRecord> RecordHealthCheckAsync(
        RecordHealthCheckRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a health check record by its ID.
    /// </summary>
    Task<HealthCheckRecord?> GetByIdAsync(
        Guid healthCheckId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent health check records for a specific target component.
    /// </summary>
    Task<IReadOnlyList<HealthCheckRecord>> GetLatestByTargetAsync(
        string target,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent non-healthy (Degraded or Unhealthy) checks.
    /// </summary>
    Task<IReadOnlyList<HealthCheckRecord>> GetUnhealthyChecksAsync(
        CancellationToken cancellationToken = default);
}
