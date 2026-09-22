namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// A row in <c>security.retention_subjects</c> has a <c>data_category</c> or
/// <c>disposal_action</c> value that does not match any known enum member.
/// The database's own CHECK constraint (migration 137) should prevent this
/// for new writes, but this is a defense-in-depth guard for rows written by
/// a different code path or an older schema version — one corrupt row must
/// fail loudly and by itself, not silently or by throwing an unrelated
/// <see cref="FormatException"/> that aborts the whole batch read
/// (<see cref="IRetentionSubjectStore.GetPendingAsync"/>) without saying why.
/// </summary>
public sealed class RetentionSubjectCorruptDataException : Exception
{
    public Guid SubjectId { get; }

    public string FieldName { get; }

    public string RawValue { get; }

    public RetentionSubjectCorruptDataException(Guid subjectId, string fieldName, string rawValue)
        : base($"Retention subject '{subjectId}' has a corrupt '{fieldName}' value '{rawValue}' that does not match any known enum member.")
    {
        SubjectId = subjectId;
        FieldName = fieldName;
        RawValue = rawValue;
    }
}
