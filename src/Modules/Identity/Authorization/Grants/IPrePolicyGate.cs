using ALKAROS.Identity.Authorization.Policies;

namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>
/// A check consulted by <see cref="IAuthorizationGrantService"/> before the
/// policy engine runs. When any gate returns true the request cannot
/// auto-approve: a <see cref="PolicyOutcome.AutoApprove"/> is downgraded to
/// <see cref="PolicyOutcome.Escalate"/> so a manager sees it. An
/// <c>always_deny</c> policy still wins, and escalation resolvers
/// (<see cref="IEscalationResolver"/>) still run afterwards. Behavioural
/// tightening (V1-IAM-023) is the first gate.
/// </summary>
public interface IPrePolicyGate
{
    /// <summary>
    /// Whether <paramref name="request"/> must not auto-approve at
    /// <paramref name="instant"/>. Implementations may record durable state as a
    /// side effect (e.g. opening a behavioural-tightening row).
    /// </summary>
    Task<bool> ShouldForceEscalationAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default);
}
