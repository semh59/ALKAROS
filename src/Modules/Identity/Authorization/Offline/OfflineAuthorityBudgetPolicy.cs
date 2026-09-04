using ALKAROS.Identity.Authorization.Policies;

namespace ALKAROS.Identity.Authorization.Offline;

/// <summary>
/// Pure derivation of an offline authority budget from the online policy set
/// (docs/domain/authorization-model.md §5). The rule is deliberately narrow:
/// a device may self-approve offline exactly what its role's <c>auto_within</c>
/// policies already auto-approve online, snapshotted once and time-boxed —
/// never <c>always_allow</c> (unbounded) and never a permission with no policy.
/// No new authority is created offline.
/// </summary>
public static class OfflineAuthorityBudgetPolicy
{
    /// <summary>Default budget lifetime — the number in the model's §5 sketch.</summary>
    public const int DefaultTtlHours = 4;

    /// <summary>
    /// The budget lines for <paramref name="roleCode"/>: one per
    /// <see cref="PolicyMode.AutoWithin"/> policy the role holds, carrying that
    /// policy's monetary limit and count. Ordered by permission code for a
    /// stable snapshot.
    /// </summary>
    public static IReadOnlyList<OfflineAuthorityBudgetLine> LinesFor(
        string roleCode, IEnumerable<AuthorizationPolicy> policies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);
        ArgumentNullException.ThrowIfNull(policies);

        return policies
            .Where(policy => policy.Mode == PolicyMode.AutoWithin
                             && string.Equals(policy.RoleCode, roleCode, StringComparison.Ordinal))
            .OrderBy(policy => policy.PermissionCode, StringComparer.Ordinal)
            .Select(policy => new OfflineAuthorityBudgetLine(
                policy.PermissionCode, policy.LimitAmount, policy.MaxCount!.Value))
            .ToArray();
    }
}
