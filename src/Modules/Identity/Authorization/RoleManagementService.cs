namespace ALKAROS.Identity.Authorization;

/// <summary>
/// Role and permission management commands.
///
/// Command-start linearization rule (CODE-008): each command takes its
/// authorization decision exactly once, at command start, before any
/// repository mutation. The protected write is conditional on that
/// decision: when the actor is denied, the command throws
/// <see cref="AuthorizationDeniedException"/> and no mutation is executed.
/// A revocation that commits after a command started does not retroactively
/// fail that in-flight command; every command started after the revocation
/// commit observes the revoked state and is denied (fail-closed deny).
/// </summary>
public sealed class RoleManagementService : IRoleManagementService
{
    private readonly IAuthorizationService _authorization;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public RoleManagementService(
        IAuthorizationService authorization,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
    }

    public async Task AddPermissionAsync(Guid actorUserId, string code, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.PermissionsManage, cancellationToken);

        if (await _permissionRepository.GetByCodeAsync(code, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Permission '{code}' already exists.");
        }

        await _permissionRepository.AddAsync(new PermissionEntry(Guid.NewGuid(), code, name), cancellationToken);
    }

    public async Task CreateRoleAsync(Guid actorUserId, string code, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);

        if (await _roleRepository.GetByCodeAsync(code, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Role '{code}' already exists.");
        }

        await _roleRepository.AddAsync(new Role(Guid.NewGuid(), code, name), cancellationToken);
    }

    public async Task AssignPermissionAsync(Guid actorUserId, Guid roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissionCode);

        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);

        var permission = await _permissionRepository.GetByCodeAsync(permissionCode, cancellationToken)
            ?? throw new InvalidOperationException($"Permission '{permissionCode}' does not exist.");

        await _roleRepository.AssignPermissionAsync(roleId, permission.Id, cancellationToken);
    }

    public async Task RevokePermissionAsync(Guid actorUserId, Guid roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissionCode);

        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);

        var permission = await _permissionRepository.GetByCodeAsync(permissionCode, cancellationToken);
        if (permission is not null)
        {
            await _roleRepository.RevokePermissionAsync(roleId, permission.Id, cancellationToken);
        }
    }

    public async Task AssignUserAsync(Guid actorUserId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);

        await _roleRepository.AssignUserAsync(userId, roleId, cancellationToken);
    }

    public async Task RevokeUserAsync(Guid actorUserId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);

        await _roleRepository.RevokeUserAsync(userId, roleId, cancellationToken);
    }
}