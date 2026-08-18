namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Base domain exception for Observability Foundation (V1-OBS-001).
/// </summary>
public abstract class ObservabilityException : Exception
{
    protected ObservabilityException(string message) : base(message) { }
    protected ObservabilityException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when attempting to persist observability data without an approved retention policy ID (V1-OBS-001).
/// </summary>
public sealed class UnapprovedRetentionPolicyException : ObservabilityException
{
    public UnapprovedRetentionPolicyException(string policyId)
        : base($"Retention policy '{policyId}' is not approved. Observability persistence without an approved retention policy is strictly rejected.")
    {
        PolicyId = policyId;
    }

    public string PolicyId { get; }
}

/// <summary>
/// Thrown when a requested health check record is not found.
/// </summary>
public sealed class HealthCheckNotFoundException : ObservabilityException
{
    public HealthCheckNotFoundException(Guid healthCheckId)
        : base($"Health check with ID '{healthCheckId}' was not found.")
    {
        HealthCheckId = healthCheckId;
    }

    public Guid HealthCheckId { get; }
}
