namespace ALKAROS.Identity.Authorization;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task RevokePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task AssignUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task RevokeUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-408: the role that governs a multi-role user's grant requests — the most restrictive one, i.e. the
    /// role holding the fewest permissions outright; ties go to the ordinally smallest role code. Null when the user
    /// has no role (PO decision 2026-09-28).
    /// </summary>
    Task<Role?> GetGoverningRoleForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPermissionCodesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<(bool Exists, bool Active)> GetUserStateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>True when a user with this exact username already exists.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new active user account with a pre-hashed password. Throws
    /// <see cref="InvalidOperationException"/> if the username is taken —
    /// checked by the caller first, but re-enforced here (the unique index
    /// on identity.users.username is the actual invariant) against a
    /// concurrent create with the same username.
    /// </summary>
    Task<Guid> CreateUserAsync(
        string username, string passwordHash, string displayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-177: found by the 2026-09-10 Garson audit — orders.transfer-server
    /// (garson-masa hand-off, V1-RMD-111) needs a target user id, but no
    /// session below manager level had any way to list staff at all. Returns
    /// every active user except <paramref name="excludingUserId"/> (the
    /// caller themselves is never a valid hand-off target), ordered by
    /// display name. Deliberately minimal (id + display name only, no
    /// username/role/permission data) — this is reachable from a plain
    /// cashier session, not a manager one.
    /// </summary>
    Task<IReadOnlyList<(Guid UserId, string DisplayName)>> ListActiveUsersAsync(
        Guid excludingUserId, CancellationToken cancellationToken = default);
}