namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>Result of measuring one data class's off-site freshness against its approved RPO.</summary>
public sealed record RpoCoverageResult(
    DataClass DataClass,
    TimeSpan Target,
    TimeSpan? MeasuredGap,
    bool MeetsTarget);

/// <summary>
/// Measures the age of the newest off-site receipt per <see cref="DataClass"/>
/// against <see cref="RpoTargets"/> (V0-BKP-002). "Oldest recoverable point"
/// is <c>now - newest receipt's UploadedAtUtc</c>: the actual data loss
/// window if the live database were lost right now.
/// </summary>
public static class RpoCoverageChecker
{
    public static RpoCoverageResult Evaluate(
        DataClass dataClass,
        IEnumerable<OffsiteBackupReceipt> receipts,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(receipts);

        var target = RpoTargets.For(dataClass);
        var newest = receipts
            .Where(r => r.DataClass == dataClass)
            .Select(r => (DateTimeOffset?)r.UploadedAtUtc)
            .Max();

        if (newest is null)
            return new RpoCoverageResult(dataClass, target, MeasuredGap: null, MeetsTarget: false);

        var gap = now - newest.Value;
        return new RpoCoverageResult(dataClass, target, gap, MeetsTarget: gap <= target);
    }
}
