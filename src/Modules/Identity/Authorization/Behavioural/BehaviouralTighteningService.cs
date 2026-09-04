namespace ALKAROS.Identity.Authorization.Behavioural;

public sealed class BehaviouralTighteningService : IBehaviouralTighteningService
{
    private readonly IBehaviouralTighteningRepository _tightenings;
    private readonly Func<DateTimeOffset> _nowUtc;

    public BehaviouralTighteningService(
        IBehaviouralTighteningRepository tightenings, Func<DateTimeOffset>? nowUtc = null)
    {
        _tightenings = tightenings ?? throw new ArgumentNullException(nameof(tightenings));
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<BehaviouralTightening>> ListActiveAsync(
        CancellationToken cancellationToken = default)
        => _tightenings.ListActiveAsync(cancellationToken);

    public Task<BehaviouralTightening> ClearAsync(
        Guid tighteningId, Guid managerUserId, CancellationToken cancellationToken = default)
    {
        if (managerUserId == Guid.Empty)
            throw new ArgumentException("A clearing manager is required.", nameof(managerUserId));

        return _tightenings.ClearAsync(tighteningId, managerUserId, _nowUtc(), cancellationToken);
    }
}
