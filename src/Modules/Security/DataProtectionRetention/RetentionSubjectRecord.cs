using ALKAROS.SensitiveData;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// A single row of <c>security.retention_subjects</c>: a sensitive envelope
/// under generic retention-execution control, plus its lifecycle state.
/// </summary>
public sealed record RetentionSubjectRecord
{
    public Guid Id { get; }
    public DataCategory Category { get; }
    public SensitiveEnvelope Envelope { get; }
    public DateTimeOffset CreatedAt { get; }
    public bool LegalHold { get; }
    public DateTimeOffset? DisposedAt { get; }
    public DisposalAction? Action { get; }
    public int RowVersion { get; }

    public RetentionSubjectRecord(
        Guid id,
        DataCategory category,
        SensitiveEnvelope envelope,
        DateTimeOffset createdAt,
        bool legalHold,
        DateTimeOffset? disposedAt,
        DisposalAction? action,
        int rowVersion)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (id == Guid.Empty)
            throw new ArgumentException("Id must not be empty.", nameof(id));
        if (rowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(rowVersion), rowVersion, "Row version must be a positive integer.");
        if ((disposedAt is null) != (action is null))
            throw new ArgumentException("DisposedAt and Action must both be set, or both be null.");

        Id = id;
        Category = category;
        Envelope = envelope;
        CreatedAt = createdAt;
        LegalHold = legalHold;
        DisposedAt = disposedAt;
        Action = action;
        RowVersion = rowVersion;
    }

    public bool IsDisposed => DisposedAt is not null;

    public RetentionSubjectSnapshot ToSnapshot() =>
        new(Id, Category, CreatedAt, LegalHold, DisposedAt, Action);
}
