using ALKAROS.SensitiveData;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Persistence for retention subjects (<c>security.retention_subjects</c>).
/// Every mutation is optimistic-concurrency-checked against the caller's
/// last-known <see cref="RetentionSubjectRecord.RowVersion"/>.
/// </summary>
public interface IRetentionSubjectStore
{
    /// <summary>Inserts a new subject and returns its generated id.</summary>
    Task<Guid> InsertAsync(
        DataCategory category,
        SensitiveEnvelope envelope,
        bool legalHold,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    Task<RetentionSubjectRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Not-yet-disposed subjects, oldest first.</summary>
    Task<IReadOnlyList<RetentionSubjectRecord>> GetPendingAsync(CancellationToken cancellationToken);

    /// <summary>Ids currently queued for hard deletion (disposed with <see cref="DataProtectionRetention.DisposalAction.Delete"/>).</summary>
    Task<IReadOnlyList<Guid>> GetDeletionQueueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks a subject Anonymize-disposed and overwrites its envelope with a
    /// non-decryptable sentinel in the same statement, or marks it
    /// Delete-disposed leaving the envelope in place for
    /// <see cref="DeletionQueueProcessor"/>. Throws
    /// <see cref="RetentionConcurrencyException"/> if <paramref name="expectedRowVersion"/>
    /// is stale.
    /// </summary>
    Task MarkDisposedAsync(
        Guid id,
        DisposalAction action,
        int expectedRowVersion,
        CancellationToken cancellationToken);

    /// <summary>Replaces a live (not yet disposed) subject's envelope — used by re-encryption.</summary>
    Task ReplaceEnvelopeAsync(
        Guid id,
        SensitiveEnvelope envelope,
        int expectedRowVersion,
        CancellationToken cancellationToken);

    Task SetLegalHoldAsync(
        Guid id,
        bool legalHold,
        int expectedRowVersion,
        CancellationToken cancellationToken);

    /// <summary>Hard-deletes a queued (Delete-disposed) row. A no-op, not an error, if the row is already gone.</summary>
    Task PurgeAsync(Guid id, CancellationToken cancellationToken);
}
