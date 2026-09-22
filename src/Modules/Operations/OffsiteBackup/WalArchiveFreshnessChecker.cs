namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>Result of comparing the newest local WAL segment against the newest one shipped off-site.</summary>
public sealed record WalArchiveFreshnessResult(
    string? NewestLocalSegment,
    string? NewestOffsiteSegment,
    TimeSpan? PositionGap,
    bool MeetsFiscalRpo);

/// <summary>
/// Measures how far the off-site copy of the WAL archive
/// (<c>alkaros-wal-archive</c>, <c>V1-RMD-095</c>) lags the local archive —
/// the concrete number the Fiscal RPO target (5 minutes,
/// <c>docs/recovery/rpo-rto-targets.md</c>) is measured against. WAL segment
/// file names are monotonically increasing (timeline/LSN encoded in the
/// name), so lexicographic order gives "newest" without parsing the
/// PostgreSQL WAL filename format.
/// </summary>
public static class WalArchiveFreshnessChecker
{
    public static WalArchiveFreshnessResult Evaluate(
        IReadOnlyCollection<string> localWalSegmentNames,
        IReadOnlyCollection<string> offsiteWalSegmentNames)
    {
        ArgumentNullException.ThrowIfNull(localWalSegmentNames);
        ArgumentNullException.ThrowIfNull(offsiteWalSegmentNames);

        var newestLocal = localWalSegmentNames.OrderBy(n => n, StringComparer.Ordinal).LastOrDefault();
        var newestOffsite = offsiteWalSegmentNames.OrderBy(n => n, StringComparer.Ordinal).LastOrDefault();

        if (newestLocal is null)
            return new WalArchiveFreshnessResult(null, newestOffsite, PositionGap: null, MeetsFiscalRpo: false);

        // The off-site copy is at or ahead of local: fully caught up.
        if (newestOffsite is not null && string.CompareOrdinal(newestOffsite, newestLocal) >= 0)
            return new WalArchiveFreshnessResult(newestLocal, newestOffsite, TimeSpan.Zero, MeetsFiscalRpo: true);

        // Segments not yet shipped off-site, in ascending order.
        var pending = localWalSegmentNames
            .Where(n => string.CompareOrdinal(n, newestOffsite ?? string.Empty) > 0)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // archive_timeout=300s (docs/recovery/rpo-rto-targets.md) forces a
        // new segment at least every 5 minutes, so N pending segments is at
        // least N * 5 minutes of lag — a conservative lower bound, never an
        // understatement of the real gap.
        var gap = TimeSpan.FromMinutes(5 * pending.Length);
        return new WalArchiveFreshnessResult(newestLocal, newestOffsite, gap, MeetsFiscalRpo: gap <= RpoTargets.For(DataClass.Fiscal));
    }
}
