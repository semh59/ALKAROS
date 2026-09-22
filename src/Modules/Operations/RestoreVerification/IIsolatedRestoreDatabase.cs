namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// A disposable, isolated PostgreSQL database provisioned for exactly one
/// restore drill. Disposing drops it — a drill never leaves state behind
/// for the next one, and never touches a live application database.
/// </summary>
public interface IIsolatedRestoreDatabase : IAsyncDisposable
{
    /// <summary>The provisioned scratch database's name, for diagnostics only.</summary>
    string Name { get; }

    /// <summary>Applies a plaintext SQL script (the decrypted restore artifact) to this database.</summary>
    Task ApplyScriptAsync(string sqlScript, CancellationToken cancellationToken = default);

    /// <summary>Runs a scalar query against this database (integrity checks, application smoke check).</summary>
    Task<object?> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default);
}

/// <summary>Provisions a fresh <see cref="IIsolatedRestoreDatabase"/> per restore drill.</summary>
public interface IIsolatedRestoreDatabaseFactory
{
    Task<IIsolatedRestoreDatabase> ProvisionAsync(CancellationToken cancellationToken = default);
}
