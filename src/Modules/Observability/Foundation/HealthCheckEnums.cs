namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Canonical health status for system components and external integrations (V1-OBS-001, PDF:II.5.13, PDF:III.28.1).
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Catalog of approved retention policy identifiers for bounded observability persistence (V1-OBS-001, PDF:III.28.1).
/// Persistence without an approved retention policy ID is rejected.
/// </summary>
public static class RetentionPolicyCatalog
{
    public const string HotOperational7D = "HOT_7D";
    public const string StandardOperational30D = "OPERATIONAL_30D";
    public const string ComplianceAudit90D = "AUDIT_90D";
    public const string ExtendedAudit365D = "AUDIT_365D";

    private static readonly HashSet<string> ApprovedPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        HotOperational7D,
        StandardOperational30D,
        ComplianceAudit90D,
        ExtendedAudit365D
    };

    public static bool IsApproved(string? policyId)
    {
        return !string.IsNullOrWhiteSpace(policyId) && ApprovedPolicies.Contains(policyId.Trim());
    }
}
