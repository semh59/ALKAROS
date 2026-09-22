namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Redacted operational-diagnostics view of a retention subject. Carries no
/// envelope, ciphertext or plaintext — safe to log or return from a
/// diagnostics surface.
/// </summary>
public sealed record RetentionSubjectSnapshot(
    Guid Id,
    DataCategory Category,
    DateTimeOffset CreatedAt,
    bool LegalHold,
    DateTimeOffset? DisposedAt,
    DisposalAction? Action);
