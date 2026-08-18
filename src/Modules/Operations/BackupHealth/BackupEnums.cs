namespace ALKAROS.Operations.BackupHealth;

/// <summary>
/// Type of database backup (V1-OPS-002, PDF:III.25).
/// </summary>
public enum BackupType
{
    Full,
    Incremental,
    SchemaOnly
}

/// <summary>
/// Lifecycle status of a backup operation (V1-OPS-002).
/// </summary>
public enum BackupStatus
{
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Status of system components in health snapshots (V1-OPS-002, PDF:II.2.23).
/// </summary>
public enum SystemHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}
