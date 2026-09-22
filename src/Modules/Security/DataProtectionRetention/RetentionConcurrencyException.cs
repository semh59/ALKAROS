namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// A write targeted a stale <c>row_version</c> — another writer changed the
/// subject first. The caller must re-read and decide whether to retry.
/// </summary>
public sealed class RetentionConcurrencyException : Exception
{
    public Guid SubjectId { get; }

    public RetentionConcurrencyException(Guid subjectId)
        : base($"Retention subject '{subjectId}' was modified by another writer; row version is stale.")
    {
        SubjectId = subjectId;
    }
}
