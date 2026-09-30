using ALKAROS.Identity.Authentication;

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
    /// <summary>
    /// Minimum password length for a new staff account. No prior policy
    /// existed anywhere in the codebase (checked); this is a baseline
    /// judgment call, not a re-derivation of an existing decision.
    /// </summary>
    private const int MinimumPasswordLength = 8;

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

    public async Task<Guid> CreateRoleAsync(Guid actorUserId, string code, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);

        if (await _roleRepository.GetByCodeAsync(code, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Role '{code}' already exists.");
        }

        var role = new Role(Guid.NewGuid(), code, name);
        await _roleRepository.AddAsync(role, cancellationToken);
        return role.Id;
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

    public async Task<Guid> CreateUserAsync(
        Guid actorUserId, string username, string password, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (username.Length > 100)
            throw new ArgumentException("Username must be at most 100 characters.", nameof(username));
        if (displayName.Length > 200)
            throw new ArgumentException("Display name must be at most 200 characters.", nameof(displayName));
        if (password.Length < MinimumPasswordLength)
            throw new ArgumentException($"Password must be at least {MinimumPasswordLength} characters.", nameof(password));

        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.UsersManage, cancellationToken);

        if (await _roleRepository.UsernameExistsAsync(username, cancellationToken))
            throw new InvalidOperationException($"Username '{username}' already exists.");

        var passwordHash = new PasswordHasher().Hash(password);
        return await _roleRepository.CreateUserAsync(username, passwordHash, displayName, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleListing>> ListRolesAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);
        return await _roleRepository.ListRolesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionEntry>> ListPermissionsAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.RolesManage, cancellationToken);
        return await _permissionRepository.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserListing>> ListUsersAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await _authorization.AuthorizeAsync(actorUserId, PermissionCodes.UsersManage, cancellationToken);
        return await _roleRepository.ListUsersAsync(cancellationToken);
    }
}
