using ALKAROS.Audit.EventStore;
using ALKAROS.CustomerData.Profiles;

namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// V14-CST-002. Owns the request/state-machine layer on top of
/// `V14-CST-001`'s own `ICustomerProfileStore.AnonymizeAsync` primitive -
/// this service decides WHEN a customer may actually be anonymized (the
/// retention guard, the request's own state), `AnonymizeAsync` decides HOW
/// (which fields get wiped). Scheduling/triggering `ExecuteAsync`
/// automatically (a scheduled retention sweep) is explicitly out of this
/// task's own scope - a caller (an admin action today, a scheduled job
/// once one is designed) invokes it.
/// </summary>
public sealed class CustomerAnonymizationService
{
    private readonly ICustomerAnonymizationRequestStore _requests;
    private readonly ICustomerProfileStore _profiles;
    private readonly IAnonymizationRetentionGuard _guard;
    private readonly IAuditEventStore _audit;

    public CustomerAnonymizationService(
        ICustomerAnonymizationRequestStore requests,
        ICustomerProfileStore profiles,
        IAnonymizationRetentionGuard guard,
        IAuditEventStore audit)
    {
        _requests = requests ?? throw new ArgumentNullException(nameof(requests));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <summary>
    /// Requested -&gt; {RetentionBlocked, Pending}, resolved synchronously by
    /// the guard check in this one call - a stored row is always already
    /// resolved (see <see cref="AnonymizationRequestStatus.Requested"/>'s own
    /// doc comment).
    /// </summary>
    public async Task<CustomerAnonymizationRequest> RequestAsync(
        Guid customerId,
        string? requestedBy,
        CancellationToken cancellationToken = default)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var blockingReason = await _guard.CheckAsync(customerId, cancellationToken);
        var initialStatus = blockingReason is null
            ? AnonymizationRequestStatus.Pending
            : AnonymizationRequestStatus.RetentionBlocked;

        var id = await _requests.CreateAsync(customerId, initialStatus, requestedBy, blockingReason, requestedAt, cancellationToken);

        await _audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(),
                blockingReason is null ? "CustomerAnonymizationRequested" : "CustomerAnonymizationBlocked",
                "CustomerAnonymizationRequest",
                id,
                actorType: requestedBy is null ? "System" : "User",
                correlationId: id.ToString(),
                reason: blockingReason),
            cancellationToken);

        return (await _requests.GetAsync(id, cancellationToken))!;
    }

    /// <summary>
    /// RetentionBlocked -&gt; Pending, only if the guard no longer reports a
    /// blocker. Idempotent no-op (returns the request unchanged) if it is
    /// not currently RetentionBlocked, or if the blocker has not cleared -
    /// a caller re-checking on a schedule must never fail just because
    /// nothing has changed yet.
    /// </summary>
    public async Task<CustomerAnonymizationRequest> ReevaluateAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetAsync(requestId, cancellationToken)
            ?? throw new CustomerAnonymizationRequestNotFoundException(requestId);
        if (request.Status != AnonymizationRequestStatus.RetentionBlocked)
            return request;

        var blockingReason = await _guard.CheckAsync(request.CustomerId, cancellationToken);
        if (blockingReason is not null)
            return request;

        await _requests.UpdateStatusAsync(
            requestId, AnonymizationRequestStatus.Pending, blockedReason: null, anonymizedAt: null, request.RowVersion, cancellationToken);

        await _audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(),
                "CustomerAnonymizationUnblocked",
                "CustomerAnonymizationRequest",
                requestId,
                actorType: "System",
                correlationId: requestId.ToString()),
            cancellationToken);

        return (await _requests.GetAsync(requestId, cancellationToken))!;
    }

    /// <summary>
    /// Pending -&gt; Anonymized. Delegates the actual field removal to
    /// `ICustomerProfileStore.AnonymizeAsync`, which wipes exactly the
    /// configured PII fields and keeps the record/id (this task's own
    /// Acceptance evidence: an approved request removes only the
    /// configured fields). Idempotent: already-Anonymized returns
    /// unchanged rather than failing.
    /// </summary>
    public async Task<CustomerAnonymizationRequest> ExecuteAsync(
        Guid requestId,
        int expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetAsync(requestId, cancellationToken)
            ?? throw new CustomerAnonymizationRequestNotFoundException(requestId);
        if (request.Status == AnonymizationRequestStatus.Anonymized)
            return request;

        if (!CustomerAnonymizationTransitions.CanTransition(request.Status, AnonymizationRequestStatus.Anonymized))
            throw new CustomerAnonymizationInvalidTransitionException(request.Status, AnonymizationRequestStatus.Anonymized);

        // Manager role here is an internal system read, not a leak surface:
        // only RowVersion/Anonymized are used below, the PII values
        // themselves (already redacted for every other role) are discarded.
        var profile = await _profiles.GetAsync(request.CustomerId, CustomerAccessRole.Manager, cancellationToken)
            ?? throw new CustomerProfileNotFoundException(request.CustomerId);
        if (!profile.Anonymized)
            await _profiles.AnonymizeAsync(request.CustomerId, profile.RowVersion, cancellationToken);

        var anonymizedAt = DateTimeOffset.UtcNow;
        await _requests.UpdateStatusAsync(
            requestId, AnonymizationRequestStatus.Anonymized, blockedReason: null, anonymizedAt, expectedRowVersion, cancellationToken);

        await _audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(),
                "CustomerAnonymized",
                "CustomerAnonymizationRequest",
                requestId,
                actorType: "System",
                correlationId: requestId.ToString()),
            cancellationToken);

        return (await _requests.GetAsync(requestId, cancellationToken))!;
    }
}
