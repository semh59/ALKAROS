namespace ALKAROS.Identity.Authorization.Behavioural;

/// <summary>The outcome of comparing a user's recent grant rate to their baseline.</summary>
public sealed record BehaviouralRateAssessment(
    int RecentCount,
    decimal BaselinePerWindow,
    decimal Ratio,
    bool IsSpiking);

/// <summary>
/// Pure rate comparison (docs/domain/authorization-model.md §1). The caller
/// supplies the granted count in the trailing window and over the 30-day
/// baseline period; this scales the baseline to the window and reports whether
/// the recent rate is a spike.
/// </summary>
public static class BehaviouralRateAssessor
{
    /// <summary>A spike is <c>recent &gt;= SpikeMultiple x baseline</c>.</summary>
    public const int SpikeMultiple = 3;

    /// <summary>
    /// Below this many actions in the window nothing is a spike, so a user with a
    /// near-zero baseline is not tightened by one or two actions.
    /// </summary>
    public const int MinimumRecentActions = 3;

    /// <summary>Baseline days the 30-day count covers.</summary>
    public const int BaselineDays = 30;

    public static BehaviouralRateAssessment Assess(
        int recentCount, int baselinePeriodCount, TimeSpan window)
    {
        if (recentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(recentCount), recentCount, "Count must not be negative.");
        if (baselinePeriodCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(baselinePeriodCount), baselinePeriodCount, "Count must not be negative.");
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be positive.");

        var perDay = baselinePeriodCount / (decimal)BaselineDays;
        var baselinePerWindow = perDay * (decimal)(window.TotalHours / 24d);

        // Floor the baseline so that hitting MinimumRecentActions in an
        // otherwise-quiet window reads as exactly SpikeMultiple, never infinity.
        var effectiveBaseline = Math.Max(
            baselinePerWindow, MinimumRecentActions / (decimal)SpikeMultiple);
        var ratio = recentCount / effectiveBaseline;

        var isSpiking = recentCount >= MinimumRecentActions && ratio >= SpikeMultiple;
        return new BehaviouralRateAssessment(recentCount, baselinePerWindow, ratio, isSpiking);
    }
}
