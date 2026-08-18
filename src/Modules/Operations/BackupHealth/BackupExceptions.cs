namespace ALKAROS.Operations.BackupHealth;

/// <summary>
/// Base exception for backup and operations health domain (V1-OPS-002).
/// </summary>
public abstract class BackupHealthException : Exception
{
    protected BackupHealthException(string message) : base(message) { }
    protected BackupHealthException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a specified backup record is not found.
/// </summary>
public sealed class BackupNotFoundException : BackupHealthException
{
    public BackupNotFoundException(Guid backupId)
        : base($"Backup record with ID '{backupId}' was not found.")
    {
        BackupId = backupId;
    }

    public Guid BackupId { get; }
}

/// <summary>
/// Thrown when backup creation fails or encounters I/O or checksum errors.
/// </summary>
public sealed class BackupExecutionException : BackupHealthException
{
    public BackupExecutionException(string message) : base(message) { }
    public BackupExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a backup file's computed checksum does not match the expected value.
/// </summary>
public sealed class InvalidChecksumException : BackupHealthException
{
    public InvalidChecksumException(string expected, string actual)
        : base($"Backup checksum mismatch. Expected: '{expected}', Actual: '{actual}'.")
    {
        Expected = expected;
        Actual = actual;
    }

    public string Expected { get; }
    public string Actual { get; }
}
