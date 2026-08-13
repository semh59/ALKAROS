using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-IAM-002 identity authorization schemas.
/// </summary>
public sealed class AuthorizationTestDatabase : PgTestDatabase
{
    public AuthorizationTestDatabase()
        : base("alkaros_iam002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<Guid> InsertUserAsync(string username, bool active = true)
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active)
            VALUES
                (@user_id, @username, @password_hash, @display_name, @active);
            """,
            ("user_id", userId),
            ("username", username),
            ("password_hash", "pbkdf2-sha256$600000$not-used-in-tests"),
            ("display_name", username),
            ("active", active));

        return userId;
    }

    public async Task<Guid> SeedRoleWithPermissionAsync(string roleCode, Guid userId, params string[] permissionCodes)
    {
        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.roles (role_id, code, name)
            VALUES (@role_id, @code, @name);
            """,
            ("role_id", roleId),
            ("code", roleCode),
            ("name", roleCode));

        foreach (var permissionCode in permissionCodes)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @rp_id, @role_id, p.permission_id
                FROM identity.permissions p
                WHERE p.code = @permission_code;
                """,
                ("rp_id", Guid.NewGuid()),
                ("role_id", roleId),
                ("permission_code", permissionCode));
        }

        await ExecuteAsync(
            """
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            VALUES (@ur_id, @user_id, @role_id);
            """,
            ("ur_id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_id", roleId));

        return roleId;
    }
}