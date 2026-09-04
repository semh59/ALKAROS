using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Grants;

namespace ALKAROS.Identity.Authorization.Behavioural;

/// <summary>
/// The pre-policy gate that implements behavioural tightening
/// (docs/domain/authorization-model.md §1). For the monetary grant-class
/// permissions it forces escalation while a tightening is open for the
/// requester, and opens one itself when the requester's recent granted rate
/// spikes past their 30-day baseline.
/// </summary>
public sealed class BehaviouralTighteningGate : IPrePolicyGate
{
    /// <summary>The trailing window the recent rate is measured over.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private static readonly HashSet<string> Monitored = new(StringComparer.Ordinal)
    {
        ApplicationPermissions.BillsVoid,
        ApplicationPermissions.BillsComp,
        ApplicationPermissions.BillsDiscount,
    };

    private readonly IBehaviouralTighteningRepository _tightenings;
    private readonly IBehaviouralRateSource _rates;

    public BehaviouralTighteningGate(
        IBehaviouralTighteningRepository tightenings, IBehaviouralRateSource rates)
    {
        _tightenings = tightenings ?? throw new ArgumentNullException(nameof(tightenings));
        _rates = rates ?? throw new ArgumentNullException(nameof(rates));
    }

    public async Task<bool> ShouldForceEscalationAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Monitored.Contains(request.PermissionCode))
            return false;

        var active = await _tightenings.FindActiveAsync(
            request.RequesterUserId, request.PermissionCode, cancellationToken);
        if (active is not null)
            return true;

        var windowStart = instant - Window;
        var lastClear = await _tightenings.MostRecentClearAsync(
            request.RequesterUserId, request.PermissionCode, cancellationToken);
        if (lastClear is { } cleared && cleared > windowStart)
            windowStart = cleared;

        // Nothing has elapsed since the last clear yet — no rate to judge.
        if (windowStart >= instant)
            return false;

        var recent = await _rates.CountGrantedSinceAsync(
            request.RequesterUserId, request.PermissionCode, windowStart, cancellationToken);
        var baselinePeriod = await _rates.CountGrantedSinceAsync(
            request.RequesterUserId,
            request.PermissionCode,
            instant.AddDays(-BehaviouralRateAssessor.BaselineDays),
            cancellationToken);

        var assessment = BehaviouralRateAssessor.Assess(recent, baselinePeriod, instant - windowStart);
        if (!assessment.IsSpiking)
            return false;

        await _tightenings.OpenAsync(
            new BehaviouralTightening(
                Guid.Empty,
                request.RequesterUserId,
                request.PermissionCode,
                assessment.RecentCount,
                assessment.BaselinePerWindow,
                assessment.Ratio,
                instant,
                ClearedAt: null,
                ClearedByUserId: null),
            cancellationToken);
        return true;
    }
}
