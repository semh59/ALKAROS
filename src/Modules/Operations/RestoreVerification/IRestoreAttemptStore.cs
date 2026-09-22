using ALKAROS.Operations.OffsiteBackup;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>Append-only persistence for <see cref="RestoreAttemptRecord"/> — a drill's history is never edited.</summary>
public interface IRestoreAttemptStore
{
    Task RecordAsync(RestoreAttemptRecord attempt, CancellationToken cancellationToken = default);

    /// <summary>Every recorded attempt for one data class, newest first, bounded by <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<RestoreAttemptRecord>> GetByDataClassAsync(DataClass dataClass, int limit = 500, CancellationToken cancellationToken = default);
}
