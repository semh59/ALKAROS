namespace ALKAROS.Identity.Authorization;

/// <summary>
/// Canonical permission codes. Every protected command in the system maps to
/// exactly one named permission from this catalog (V1-IAM-002 acceptance:
/// "her korunan komutun adlandırılmış bir izni vardır").
/// </summary>
public static class PermissionCodes
{
    public const string UsersManage = "identity.users.manage";

    public const string RolesManage = "identity.roles.manage";

    public const string PermissionsManage = "identity.permissions.manage";

    public const string DeviceSessionsManage = "identity.device_sessions.manage";
}

public sealed record PermissionEntry(Guid Id, string Code, string Name);

public sealed record Role(Guid Id, string Code, string Name);