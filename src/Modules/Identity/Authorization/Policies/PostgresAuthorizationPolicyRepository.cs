using Npgsql;

namespace ALKAROS.Identity.Authorization.Policies;

public sealed class PostgresAuthorizationPolicyRepository : IAuthorizationPolicyRepository
{
    private const string Table = "identity.authorization_policies";
    private const int MaxUnpagedRows = 5000;
    private const string UniqueViolation = "23505";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuthorizationPolicyRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AuthorizationPolicy?> GetAsync(
        string permissionCode, string roleCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT policy_id, permission_code, role_code, mode,
                   limit_amount, max_count, window_seconds, row_version
            FROM {Table}
            WHERE permission_code = @permission AND role_code = @role;
            """);
        command.Parameters.AddWithValue("permission", permissionCode);
        command.Parameters.AddWithValue("role", roleCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<AuthorizationPolicy>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<AuthorizationPolicy>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT policy_id, permission_code, role_code, mode,
                   limit_amount, max_count, window_seconds, row_version
            FROM {Table}
            ORDER BY permission_code, role_code
            LIMIT {MaxUnpagedRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Read(reader));

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"{Table} has more than {MaxUnpagedRows} rows; ListAsync must be paginated.");

        return result;
    }

    public async Task<AuthorizationPolicy> UpsertAsync(
        AuthorizationPolicy policy,
        long? expectedRowVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        // A replace (expectedRowVersion given) and a create (null) are different
        // statements, not one INSERT .. ON CONFLICT: with a single statement, a
        // replace of a scope that was concurrently deleted has nothing to
        // conflict with, so the INSERT branch runs unconditionally and silently
        // re-creates the row at row_version 1 instead of raising the
        // concurrency exception the caller's stale expectedRowVersion earned.
        return expectedRowVersion is { } expected
            ? await ReplaceAsync(policy, expected, actorUserId, cancellationToken)
            : await CreateAsync(policy, actorUserId, cancellationToken);
    }

    private async Task<AuthorizationPolicy> ReplaceAsync(
        AuthorizationPolicy policy, long expectedRowVersion, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table} SET
                mode = @mode,
                limit_amount = @limit,
                max_count = @max_count,
                window_seconds = @window,
                row_version = row_version + 1,
                updated_at = now(),
                updated_by = @actor
            WHERE permission_code = @permission AND role_code = @role AND row_version = @expected_version
            RETURNING policy_id, permission_code, role_code, mode,
                      limit_amount, max_count, window_seconds, row_version;
            """);
        AddPolicyParameters(command, policy, actorUserId);
        command.Parameters.AddWithValue("expected_version", expectedRowVersion);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new AuthorizationPolicyConcurrencyException(policy.PermissionCode, policy.RoleCode);

        return Read(reader);
    }

    private async Task<AuthorizationPolicy> CreateAsync(
        AuthorizationPolicy policy, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table}
                (permission_code, role_code, mode, limit_amount, max_count, window_seconds,
                 row_version, updated_at, updated_by)
            VALUES
                (@permission, @role, @mode, @limit, @max_count, @window, 1, now(), @actor)
            RETURNING policy_id, permission_code, role_code, mode,
                      limit_amount, max_count, window_seconds, row_version;
            """);
        AddPolicyParameters(command, policy, actorUserId);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return Read(reader);
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            // Someone else created this scope first — the same "expected state
            // no longer holds" case a stale-version replace raises.
            throw new AuthorizationPolicyConcurrencyException(policy.PermissionCode, policy.RoleCode);
        }
    }

    private static void AddPolicyParameters(NpgsqlCommand command, AuthorizationPolicy policy, Guid actorUserId)
    {
        command.Parameters.AddWithValue("permission", policy.PermissionCode);
        command.Parameters.AddWithValue("role", policy.RoleCode);
        command.Parameters.AddWithValue("mode", PolicyModeText.ToText(policy.Mode));
        command.Parameters.AddWithValue("limit", (object?)policy.LimitAmount ?? DBNull.Value);
        command.Parameters.AddWithValue("max_count", (object?)policy.MaxCount ?? DBNull.Value);
        command.Parameters.AddWithValue("window", (object?)policy.WindowSeconds ?? DBNull.Value);
        command.Parameters.AddWithValue("actor", actorUserId);
    }

    public async Task<bool> DeleteAsync(
        string permissionCode, string roleCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);

        await using var command = _dataSource.CreateCommand(
            $"DELETE FROM {Table} WHERE permission_code = @permission AND role_code = @role;");
        command.Parameters.AddWithValue("permission", permissionCode);
        command.Parameters.AddWithValue("role", roleCode);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static AuthorizationPolicy Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        PolicyModeText.FromText(reader.GetString(3)),
        reader.IsDBNull(4) ? null : reader.GetDecimal(4),
        reader.IsDBNull(5) ? null : reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetInt32(6),
        reader.GetInt64(7));
}
