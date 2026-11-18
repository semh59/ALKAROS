using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Experience.Authorization;
using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authorization;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>The approving manager is the person who asked; a second person has to approve at the till.</summary>
public sealed class InPlaceSelfApprovalException() : Exception("The approving manager is the requester.");

/// <summary>
/// A manager at the till approves with their own username and password: the same sign-in check, per-cashier and per-manager
/// attempt limit, failure audit and permission check for every in-place approval. The caller decides when a finished
/// approval clears the attempt counts (<see cref="Confirm"/>), because a correct password that is then refused proves nothing.
/// </summary>
public sealed class InPlaceManagerApproval(
    AuthenticationService authentication, IAuthorizationService authorization, InPlaceApprovalThrottle throttle, IAuditEventStore auditEvents)
{
    public async Task<LoginSuccess> VerifyAsync(
        string? username, string? password, Guid requesterId, string permission, string failureEvent, string aggregateType,
        Guid aggregateId, string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || username.Length > 64 || password.Length > 256)
            throw new ArgumentException("Manager username (at most 64) and password (at most 256) are both required.");

        username = username.Trim();
        var now = DateTimeOffset.UtcNow;
        if (throttle.IsBlocked(CashierKey(requesterId), now) || throttle.IsBlocked(ManagerKey(username), now))
            throw new InPlaceApprovalThrottledException();

        var login = await authentication.LoginAsync(username, password, now, cancellationToken);
        if (login is not LoginSuccess manager)
        {
            var lockedOut = login is LoginFailure { Reason: LoginFailureReason.LockedOut };
            throttle.RecordFailure(CashierKey(requesterId), now);
            throttle.RecordFailure(ManagerKey(username), now);
            // A password typed into the username box must not reach the audit trail, so only the first two characters are kept.
            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(), eventName: failureEvent, aggregateType: aggregateType, aggregateId: aggregateId,
                    actorType: "User", correlationId: correlationId, actorId: requesterId,
                    reason: lockedOut ? "manager_locked" : "credential_invalid",
                    afterStateJson: JsonSerializer.Serialize(new
                    {
                        attemptedUsername = username[..Math.Min(2, username.Length)] + "***", attemptedUsernameLength = username.Length,
                    })),
                cancellationToken);
            throw new ManagerCredentialException(lockedOut);
        }

        if (manager.UserId == requesterId)
            throw new InPlaceSelfApprovalException();
        try
        {
            await authorization.AuthorizeAsync(manager.UserId, permission, cancellationToken);
        }
        catch (AuthorizationDeniedException)
        {
            throw new ApproverLacksPermissionException(permission);
        }

        return manager;
    }

    public void Confirm(Guid requesterId, string username)
    {
        throttle.Reset(CashierKey(requesterId));
        throttle.Reset(ManagerKey(username.Trim()));
    }

    private static string CashierKey(Guid requesterId) => $"cashier:{requesterId:N}";

    private static string ManagerKey(string username) => $"manager:{username.ToUpperInvariant()}";
}
