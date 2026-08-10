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
}