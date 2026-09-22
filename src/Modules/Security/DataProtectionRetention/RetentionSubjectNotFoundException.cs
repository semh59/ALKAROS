namespace ALKAROS.Security.DataProtectionRetention;

public sealed class RetentionSubjectNotFoundException : Exception
{
    public Guid SubjectId { get; }

    public RetentionSubjectNotFoundException(Guid subjectId)
        : base($"Retention subject '{subjectId}' was not found.")
    {
        SubjectId = subjectId;
    }
}
