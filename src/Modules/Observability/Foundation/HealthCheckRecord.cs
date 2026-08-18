namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Immutable domain record for a system health check result (V1-OBS-001, PDF:III.28.1).
/// </summary>
public sealed record HealthCheckRecord(
    Guid HealthCheckId,
    string CheckType,
    string Target,
    HealthStatus Status,
    DateTimeOffset CheckedAt,
    string RetentionPolicyId,
    string? DetailsJson);

/// <summary>
/// Command request to record a health check (V1-OBS-001).
/// </summary>
public sealed record RecordHealthCheckRequest(
    string CheckType,
    string Target,
    HealthStatus Status,
    string RetentionPolicyId,
    string? DetailsJson = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CheckType))
            throw new ArgumentException("Check type cannot be null, empty, or whitespace.", nameof(CheckType));

        if (string.IsNullOrWhiteSpace(Target))
            throw new ArgumentException("Target cannot be null, empty, or whitespace.", nameof(Target));

        if (string.IsNullOrWhiteSpace(RetentionPolicyId))
            throw new ArgumentException("Retention policy ID cannot be null, empty, or whitespace.", nameof(RetentionPolicyId));
    }
}
