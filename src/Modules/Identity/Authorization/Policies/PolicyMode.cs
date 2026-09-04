namespace ALKAROS.Identity.Authorization.Policies;

/// <summary>
/// How a policy row resolves a <c>grant</c>-class authorization request for one
/// (permission, role) pair. See <c>docs/domain/authorization-model.md</c> §4.
/// </summary>
public enum PolicyMode
{
    /// <summary>Refuse before the request reaches a manager.</summary>
    AlwaysDeny,

    /// <summary>Auto-approve unconditionally.</summary>
    AlwaysAllow,

    /// <summary>
    /// Auto-approve while the monetary delta is within <c>LimitAmount</c> and the
    /// requester has fewer than <c>MaxCount</c> auto-grants in the trailing
    /// <c>WindowSeconds</c>; otherwise escalate.
    /// </summary>
    AutoWithin,
}

/// <summary>
/// Maps <see cref="PolicyMode"/> to and from the <c>identity.authorization_policies.mode</c>
/// text values. Kept beside the enum so the storage contract has one home.
/// </summary>
public static class PolicyModeText
{
    public const string AlwaysDeny = "always_deny";
    public const string AlwaysAllow = "always_allow";
    public const string AutoWithin = "auto_within";

    public static string ToText(PolicyMode mode) => mode switch
    {
        PolicyMode.AlwaysDeny => AlwaysDeny,
        PolicyMode.AlwaysAllow => AlwaysAllow,
        PolicyMode.AutoWithin => AutoWithin,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown policy mode."),
    };

    public static PolicyMode FromText(string text) => text switch
    {
        AlwaysDeny => PolicyMode.AlwaysDeny,
        AlwaysAllow => PolicyMode.AlwaysAllow,
        AutoWithin => PolicyMode.AutoWithin,
        _ => throw new ArgumentException($"Unknown policy mode '{text}'.", nameof(text)),
    };
}
