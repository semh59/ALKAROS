namespace ALKAROS.Identity.Authorization;

/// <summary>
/// Protected role and permission-management commands. Every method first
/// authorizes the actor; a denial raises
/// <see cref="AuthorizationDeniedException"/> before any write, so denied
/// actions never change state.
/// </summary>
public interface IRoleManagementService
{
    Task AddPermissionAsync(Guid actorUserId, string code, string name, CancellationToken cancellationToken = default);

    Task CreateRoleAsync(Guid actorUserId, string code, string name, CancellationToken cancellationToken = default);

    Task AssignPermissionAsync(Guid actorUserId, Guid roleId, string permissionCode, CancellationToken cancellationToken = default);

    Task RevokePermissionAsync(Guid actorUserId, Guid roleId, string permissionCode, CancellationToken cancellationToken = default);

    Task AssignUserAsync(Guid actorUserId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task RevokeUserAsync(Guid actorUserId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}