namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Thrown when an operation that requires a live envelope (re-encryption,
/// legal-hold toggling) targets a subject that has already been disposed.
/// </summary>
public sealed class RetentionSubjectDisposedException : Exception
{
    public Guid SubjectId { get; }

    public RetentionSubjectDisposedException(Guid subjectId)
        : base($"Retention subject '{subjectId}' has already been disposed.")
    {
        SubjectId = subjectId;
    }
}
