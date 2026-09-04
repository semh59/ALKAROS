namespace ALKAROS.Identity.Authorization.Policies;

/// <summary>The outcome of policy evaluation — step 1 of the grant flow.</summary>
public enum PolicyOutcome
{
    /// <summary>Approve now, no human needed. Recorded as <c>policy_path = 'auto'</c>.</summary>
    AutoApprove,

    /// <summary>Hand to step 2 (delegation) then step 3 (a manager).</summary>
    Escalate,

    /// <summary>Refuse without escalation.</summary>
    Deny,
}

/// <summary>
/// Pure evaluation of the policy engine (<c>docs/domain/authorization-model.md</c>
/// §4 step 1). No I/O: the caller supplies the matching policy row (or null) and
/// the requester's prior auto-grant count for the window.
/// </summary>
public static class AuthorizationPolicyEvaluator
{
    /// <param name="policy">
    /// The <c>(permission, role)</c> policy row, or null when none is configured.
    /// </param>
    /// <param name="requestedAmount">
    /// The absolute monetary delta of the pending action (0 when the action has
    /// no monetary effect, e.g. a table transfer).
    /// </param>
    /// <param name="priorAutoGrantsInWindow">
    /// How many auto-approved grants the requester already has for this permission
    /// inside the policy's window. Ignored unless the mode is
    /// <see cref="PolicyMode.AutoWithin"/>.
    /// </param>
    public static PolicyOutcome Evaluate(
        AuthorizationPolicy? policy,
        decimal requestedAmount,
        int priorAutoGrantsInWindow)
    {
        if (requestedAmount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(requestedAmount), requestedAmount, "Requested amount must not be negative.");
        if (priorAutoGrantsInWindow < 0)
            throw new ArgumentOutOfRangeException(
                nameof(priorAutoGrantsInWindow), priorAutoGrantsInWindow,
                "Prior auto-grant count must not be negative.");

        // Fail-closed default: nothing configured -> a manager must decide.
        if (policy is null)
            return PolicyOutcome.Escalate;

        return policy.Mode switch
        {
            PolicyMode.AlwaysDeny => PolicyOutcome.Deny,
            PolicyMode.AlwaysAllow => PolicyOutcome.AutoApprove,
            PolicyMode.AutoWithin =>
                requestedAmount <= policy.LimitAmount!.Value
                && priorAutoGrantsInWindow < policy.MaxCount!.Value
                    ? PolicyOutcome.AutoApprove
                    : PolicyOutcome.Escalate,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy), policy.Mode, "Unknown policy mode."),
        };
    }
}
