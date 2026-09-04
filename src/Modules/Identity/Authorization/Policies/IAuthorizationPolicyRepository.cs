namespace ALKAROS.Identity.Authorization.Policies;

/// <summary>
/// Reads and writes <c>identity.authorization_policies</c>. Writes are
/// last-write-wins on the <c>(permission_code, role_code)</c> scope with an
/// optimistic <c>row_version</c> check so two managers editing the same limit
/// cannot silently clobber each other.
/// </summary>
public interface IAuthorizationPolicyRepository
{
    /// <summary>The policy for one scope, or null when none is configured.</summary>
    Task<AuthorizationPolicy?> GetAsync(
        string permissionCode, string roleCode, CancellationToken cancellationToken = default);

    /// <summary>Every configured policy, ordered by permission then role.</summary>
    Task<IReadOnlyList<AuthorizationPolicy>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the policy for its scope. <paramref name="expectedRowVersion"/>
    /// is null for a create and the current version for a replace; a mismatch
    /// throws <see cref="AuthorizationPolicyConcurrencyException"/>. Returns the
    /// stored row (with its new <c>row_version</c>).
    /// </summary>
    Task<AuthorizationPolicy> UpsertAsync(
        AuthorizationPolicy policy,
        long? expectedRowVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the policy for the scope. Returns false when no row existed.
    /// </summary>
    Task<bool> DeleteAsync(
        string permissionCode, string roleCode, CancellationToken cancellationToken = default);
}

/// <summary>Raised when an <c>Upsert</c> loses the optimistic-concurrency check.</summary>
public sealed class AuthorizationPolicyConcurrencyException : Exception
{
    public AuthorizationPolicyConcurrencyException(string permissionCode, string roleCode)
        : base($"Authorization policy for '{permissionCode}'/'{roleCode}' changed since it was read.")
    {
        PermissionCode = permissionCode;
        RoleCode = roleCode;
    }

    public string PermissionCode { get; }

    public string RoleCode { get; }
}
