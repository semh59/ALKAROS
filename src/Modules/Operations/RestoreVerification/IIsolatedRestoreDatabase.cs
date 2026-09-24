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

    /// <summary>
    /// Restores a <c>pg_dump --format=custom</c> artifact (what <c>backup.sh</c> produces)
    /// with <c>pg_restore</c>. A custom-format dump is a binary archive, so it cannot be
    /// applied as SQL text through <see cref="ApplyScriptAsync"/>. The default refuses, so
    /// an implementation that cannot run <c>pg_restore</c> fails loudly, never silently.
    /// </summary>
    Task ApplyCustomFormatDumpAsync(ReadOnlyMemory<byte> dump, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This restore database cannot apply a custom-format dump.");

    /// <summary>Runs a scalar query against this database (integrity checks, application smoke check).</summary>
    Task<object?> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default);
}

/// <summary>Provisions a fresh <see cref="IIsolatedRestoreDatabase"/> per restore drill.</summary>
public interface IIsolatedRestoreDatabaseFactory
{
    Task<IIsolatedRestoreDatabase> ProvisionAsync(CancellationToken cancellationToken = default);
}
