namespace ALKAROS.Identity.Authorization;

/// <summary>
/// Default deny path: actor must exist, be active, and hold a role that grants
/// the requested permission. Denials are audited through
/// <see cref="IDenialEventSink"/> before the exception is raised.
/// </summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IDenialEventSink _denialEventSink;

    public AuthorizationService(IRoleRepository roleRepository, IDenialEventSink denialEventSink)
    {
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _denialEventSink = denialEventSink ?? throw new ArgumentNullException(nameof(denialEventSink));
    }

    public async Task AuthorizeAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissionCode);

        var (exists, active) = await _roleRepository.GetUserStateAsync(userId, cancellationToken);
        if (!exists)
        {
            await DenyAsync(null, permissionCode, "User does not exist", cancellationToken);
            return;
        }

        if (!active)
        {
            await DenyAsync(userId, permissionCode, "User is inactive", cancellationToken);
            return;
        }

        var permissionCodes = await _roleRepository.GetPermissionCodesForUserAsync(userId, cancellationToken);
        if (!permissionCodes.Contains(permissionCode, StringComparer.Ordinal))
        {
            await DenyAsync(userId, permissionCode, "No assigned role grants the permission", cancellationToken);
        }
    }

    private async Task DenyAsync(
        Guid? userId,
        string permissionCode,
        string reason,
        CancellationToken cancellationToken)
    {
        await _denialEventSink.RecordAsync(
            new DenialEvent(userId, permissionCode, reason, DateTimeOffset.UtcNow),
            cancellationToken);
        throw new AuthorizationDeniedException(userId, permissionCode, reason);
    }
}