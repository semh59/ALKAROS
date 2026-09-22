namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>Outcome of one <see cref="RetentionExecutionService.RunSweepAsync"/> pass.</summary>
public sealed record RetentionSweepResult(
    IReadOnlyList<Guid> Disposed,
    IReadOnlyList<Guid> SkippedLegalHold,
    IReadOnlyList<Guid> SkippedRetain,
    IReadOnlyList<Guid> SkippedNotExpired);
