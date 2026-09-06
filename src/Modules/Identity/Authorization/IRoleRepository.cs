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
}