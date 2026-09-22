using ALKAROS.Operations.OffsiteBackup;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// The recorded outcome of one restore drill — always written, success or
/// failure, so drill history (and RTO trend) survives the process that ran
/// it.
/// </summary>
public sealed record RestoreAttemptRecord
{
    public string ArtifactId { get; }
    public DataClass DataClass { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public TimeSpan Duration { get; }
    public bool Succeeded { get; }
    public bool WithinRtoTarget { get; }
    public int IntegrityChecksPassed { get; }
    public int IntegrityChecksTotal { get; }
    public string? FailureReason { get; }

    public RestoreAttemptRecord(
        string artifactId,
        DataClass dataClass,
        DateTimeOffset startedAtUtc,
        TimeSpan duration,
        bool succeeded,
        bool withinRtoTarget,
        int integrityChecksPassed,
        int integrityChecksTotal,
        string? failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");
        if (integrityChecksPassed < 0 || integrityChecksPassed > integrityChecksTotal)
            throw new ArgumentOutOfRangeException(nameof(integrityChecksPassed), integrityChecksPassed, "Passed count must be between 0 and the total.");
        if (!succeeded && string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("A failed attempt must record a failure reason.", nameof(failureReason));

        ArtifactId = artifactId;
        DataClass = dataClass;
        StartedAtUtc = startedAtUtc;
        Duration = duration;
        Succeeded = succeeded;
        WithinRtoTarget = withinRtoTarget;
        IntegrityChecksPassed = integrityChecksPassed;
        IntegrityChecksTotal = integrityChecksTotal;
        FailureReason = failureReason;
    }
}
