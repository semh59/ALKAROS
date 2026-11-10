using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using Npgsql;

namespace ALKAROS.Host.Experience.Authorization;

/// <summary>
/// The manager decision surface (V1-IAM-020): read the pending grants, active
/// delegations and open behavioural tightenings, and resolve them. Approve /
/// deny record <c>policy_path = 'manual'</c> with the approver; the first
/// responder wins and a second attempt raises
/// <see cref="AuthorizationGrantAlreadyResolvedException"/>.
/// </summary>
public sealed class AuthorizationDecisionStore
{
    private readonly IAuthorizationGrantRepository _grants;
    private readonly IAuthorizationDelegationRepository _delegations;
    private readonly IBehaviouralTighteningService _tightenings;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditEventStore _auditEvents;
    private readonly NpgsqlDataSource _dataSource;
    private readonly Func<DateTimeOffset> _nowUtc;

    public AuthorizationDecisionStore(
        IAuthorizationGrantRepository grants,
        IAuthorizationDelegationRepository delegations,
        IBehaviouralTighteningService tightenings,
        IAuthorizationService authorization,
        IAuditEventStore auditEvents,
        NpgsqlDataSource dataSource,
        Func<DateTimeOffset>? nowUtc = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _auditEvents = auditEvents ?? throw new ArgumentNullException(nameof(auditEvents));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
        _delegations = delegations ?? throw new ArgumentNullException(nameof(delegations));
        _tightenings = tightenings ?? throw new ArgumentNullException(nameof(tightenings));
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PendingGrantV1>> ListPendingGrantsAsync(CancellationToken cancellationToken)
    {
        var grants = await _grants.ListPendingAsync(cancellationToken);
        return grants
            .Select(grant => new PendingGrantV1(
                grant.GrantId,
                grant.PermissionCode,
                grant.RequesterUserId,
                grant.RequesterRoleCode,
                grant.SubjectType,
                grant.SubjectId,
                grant.SubjectServingUserId,
                grant.Amount,
                grant.ReasonCode,
                grant.RequestedAt))
            .ToArray();
    }

    public Task<ResolvedGrantV1> ApproveAsync(Guid grantId, Guid approverUserId, string correlationId, CancellationToken cancellationToken)
        => ResolveAsync(grantId, GrantStatus.Granted, approverUserId, correlationId, cancellationToken);

    public Task<ResolvedGrantV1> DenyAsync(Guid grantId, Guid approverUserId, string correlationId, CancellationToken cancellationToken)
        => ResolveAsync(grantId, GrantStatus.Denied, approverUserId, correlationId, cancellationToken);

    private async Task<ResolvedGrantV1> ResolveAsync(
        Guid grantId, GrantStatus status, Guid approverUserId, string correlationId, CancellationToken cancellationToken)
    {
        // V1-RMD-316 (independent 2026-09-26 audit, finding K9): RequesterUserId is immutable once a grant
        // is inserted, so this read-then-write has no meaningful race window for THIS check specifically -
        // unlike ResolveAsync's own pending->terminal transition (still the real, race-safe guard via its
        // WHERE status = 'pending'). A grant not found here behaves like AlreadyResolvedException below,
        // matching what an unconditional ResolveAsync call would have done anyway.
        var pending = await _grants.GetAsync(grantId, cancellationToken);
        if (pending is not null && pending.RequesterUserId == approverUserId)
            throw new AuthorizationSelfApprovalException(grantId, approverUserId);

        // Whoever answers must be allowed to do the thing themselves; reading reports is not enough.
        if (pending is not null)
        {
            try
            {
                await _authorization.AuthorizeAsync(approverUserId, pending.PermissionCode, cancellationToken);
            }
            catch (AuthorizationDeniedException)
            {
                throw new ApproverLacksPermissionException(pending.PermissionCode);
            }
        }

        // The grant row says who decided; the audit trail says on what, so a decision can be read back like every other
        // approval. Both commit together: a decision whose record failed is not made, and a retry can still make it.
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var resolved = await _grants.ResolveAsync(
            grantId, status, PolicyPath.Manual, approverUserId, connection, transaction, cancellationToken);
        await _auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: status == GrantStatus.Granted ? "authorization.grant.approved" : "authorization.grant.denied",
                aggregateType: "AuthorizationGrant",
                aggregateId: resolved.GrantId,
                actorType: "User",
                correlationId: correlationId,
                actorId: approverUserId,
                reason: resolved.ReasonCode,
                afterStateJson: JsonSerializer.Serialize(new
                {
                    status = GrantText.Status(resolved.Status),
                    permissionCode = resolved.PermissionCode,
                    amount = resolved.Amount,
                    subjectType = resolved.SubjectType,
                    subjectId = resolved.SubjectId,
                    requesterUserId = resolved.RequesterUserId,
                    policyPath = GrantText.Path(resolved.Path!.Value),
                })),
            connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResolvedGrantV1(
            resolved.GrantId,
            GrantText.Status(resolved.Status),
            GrantText.Path(resolved.Path!.Value),
            resolved.ApproverUserId!.Value,
            resolved.ResolvedAt!.Value);
    }

    public async Task<IReadOnlyList<ActiveDelegationV1>> ListActiveDelegationsAsync(CancellationToken cancellationToken)
    {
        var delegations = await _delegations.ListActiveAsync(_nowUtc(), cancellationToken);
        return delegations
            .Select(delegation => new ActiveDelegationV1(
                delegation.DelegationId,
                delegation.PermissionCode,
                delegation.GranteeUserId,
                delegation.DelegatorUserId,
                delegation.LimitAmount,
                delegation.GrantedAt,
                delegation.ExpiresAt))
            .ToArray();
    }

    /// <summary>
    /// V1-RMD-407 (V1-RMD-399 H-04): only grant-class permissions (model §3) are delegable, and a delegation is
    /// time-boxed to one shift at most. The delegator's own right to the permission is checked by the endpoint.
    /// </summary>
    public static readonly TimeSpan MaximumDelegationLifetime = TimeSpan.FromHours(24);

    private static readonly HashSet<string> DelegablePermissions = new(StringComparer.Ordinal)
    {
        ApplicationPermissions.BillsVoid,
        ApplicationPermissions.BillsComp,
        ApplicationPermissions.BillsDiscount,
    };

    public static bool IsDelegable(string? permissionCode)
        => permissionCode is not null && DelegablePermissions.Contains(permissionCode);

    public async Task<ActiveDelegationV1> CreateDelegationAsync(
        CreateDelegationRequestV1 request, Guid delegatorUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _nowUtc();
        if (!IsDelegable(request.PermissionCode))
            throw new ArgumentException("Only bills.void, bills.comp and bills.discount can be delegated.", nameof(request));
        if (request.ExpiresAt > now + MaximumDelegationLifetime)
            throw new ArgumentException("A delegation may last at most 24 hours.", nameof(request));

        var delegationRequest = new DelegationRequest(
            request.PermissionCode!, request.GranteeUserId, delegatorUserId, request.LimitAmount, request.ExpiresAt);
        delegationRequest.Validate(now);

        AuthorizationDelegation delegation;
        try
        {
            delegation = await _delegations.CreateAsync(delegationRequest, now, cancellationToken);
        }
        catch (Npgsql.PostgresException exception) when (exception.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new DelegationGranteeNotFoundException(request.GranteeUserId);
        }

        return new ActiveDelegationV1(
            delegation.DelegationId,
            delegation.PermissionCode,
            delegation.GranteeUserId,
            delegation.DelegatorUserId,
            delegation.LimitAmount,
            delegation.GrantedAt,
            delegation.ExpiresAt);
    }

    public Task<bool> RevokeDelegationAsync(Guid delegationId, Guid actorUserId, CancellationToken cancellationToken)
        => _delegations.RevokeAsync(delegationId, _nowUtc(), actorUserId, cancellationToken);

    public async Task<IReadOnlyList<OpenTighteningV1>> ListOpenTighteningsAsync(CancellationToken cancellationToken)
    {
        var tightenings = await _tightenings.ListActiveAsync(cancellationToken);
        return tightenings
            .Select(tightening => new OpenTighteningV1(
                tightening.TighteningId,
                tightening.UserId,
                tightening.PermissionCode,
                tightening.RecentCount,
                tightening.TriggerRatio,
                tightening.TriggeredAt))
            .ToArray();
    }

    public async Task ClearTighteningAsync(Guid tighteningId, Guid managerUserId, CancellationToken cancellationToken)
    {
        // V1-RMD-316 (independent 2026-09-26 audit, finding K9): same self-approval gap as ResolveAsync
        // above, one schema over - the user whose own spiking auto-grant rate opened this tightening must
        // never be the one who clears it. IBehaviouralTighteningRepository has no get-by-id (only
        // FindActiveAsync by scope and ListActiveAsync), so this checks against the already-open list the
        // manager surface itself reads (ListOpenTighteningsAsync) rather than adding a new repository
        // method for a single, small, already-loaded set.
        var active = await _tightenings.ListActiveAsync(cancellationToken);
        var target = active.FirstOrDefault(t => t.TighteningId == tighteningId);
        if (target is not null && target.UserId == managerUserId)
            throw new BehaviouralTighteningSelfClearException(tighteningId, managerUserId);

        await _tightenings.ClearAsync(tighteningId, managerUserId, cancellationToken);
    }
}

/// <summary>The person answering a request does not hold the permission the request asks for.</summary>
public sealed class ApproverLacksPermissionException : Exception
{
    public ApproverLacksPermissionException(string permissionCode)
        : base($"The approver does not hold '{permissionCode}'.")
    {
        PermissionCode = permissionCode;
    }

    public string PermissionCode { get; }
}
