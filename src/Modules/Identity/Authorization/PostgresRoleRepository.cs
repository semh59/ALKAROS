using Npgsql;

namespace ALKAROS.Identity.Authorization;

public sealed class PostgresRoleRepository : IRoleRepository
{
    private const string Roles = "identity.roles";
    private const string RolePermissions = "identity.role_permissions";
    private const string UserRoles = "identity.user_roles";
    private const string PermissionTable = "identity.permissions";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRoleRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT role_id, code, name
            FROM {Roles}
            WHERE role_id = @role_id;
            """);
        command.Parameters.AddWithValue("role_id", roleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRole(reader);
    }

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT role_id, code, name
            FROM {Roles}
            WHERE code = @code;
            """);
        command.Parameters.AddWithValue("code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRole(reader);
    }

    public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Roles} (role_id, code, name)
            VALUES (@role_id, @code, @name);
            """);
        command.Parameters.AddWithValue("role_id", role.Id);
        command.Parameters.AddWithValue("code", role.Code);
        command.Parameters.AddWithValue("name", role.Name);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"Role '{role.Code}' already exists.", ex);
        }
    }

    public async Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {RolePermissions} (role_permission_id, role_id, permission_id)
            VALUES (@id, @role_id, @permission_id);
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("permission_id", permissionId);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException(
                $"Permission '{permissionId}' is already assigned to role '{roleId}'.", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException(
                $"Role '{roleId}' or permission '{permissionId}' does not exist.", ex);
        }
    }

    public async Task RevokePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {RolePermissions}
            WHERE role_id = @role_id AND permission_id = @permission_id;
            """);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("permission_id", permissionId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AssignUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {UserRoles} (user_role_id, user_id, role_id)
            VALUES (@id, @user_id, @role_id);
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_id", roleId);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException(
                $"User '{userId}' already has role '{roleId}'.", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException(
                $"User '{userId}' or role '{roleId}' does not exist.", ex);
        }
    }

    public async Task RevokeUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            DELETE FROM {UserRoles}
            WHERE user_id = @user_id AND role_id = @role_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_id", roleId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new List<Guid>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT role_id
            FROM {UserRoles}
            WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0));

        return result;
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new List<string>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT DISTINCT p.code
            FROM {UserRoles} ur
            JOIN {RolePermissions} rp ON rp.role_id = ur.role_id
            JOIN {Roles} r ON r.role_id = ur.role_id
            JOIN {PermissionTable} p ON p.permission_id = rp.permission_id
            WHERE ur.user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));

        return result;
    }

    public async Task<(bool Exists, bool Active)> GetUserStateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT active FROM identity.users WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return (false, false);

        return (true, reader.GetBoolean(0));
    }

    private static Role ReadRole(NpgsqlDataReader reader)
        => new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
}