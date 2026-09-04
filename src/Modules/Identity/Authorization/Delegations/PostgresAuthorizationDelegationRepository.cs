using Npgsql;

namespace ALKAROS.Identity.Authorization.Delegations;

public sealed class PostgresAuthorizationDelegationRepository : IAuthorizationDelegationRepository
{
    private const string Table = "identity.authorization_delegations";
    private const int MaxActiveRows = 5000;

    private const string SelectColumns =
        "delegation_id, permission_code, grantee_user_id, delegator_user_id, " +
        "limit_amount, granted_at, expires_at, revoked_at, revoked_by_user_id";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuthorizationDelegationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AuthorizationDelegation> CreateAsync(
        DelegationRequest request, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate(now);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table}
                (permission_code, grantee_user_id, delegator_user_id, limit_amount, granted_at, expires_at)
            VALUES (@permission, @grantee, @delegator, @limit, @granted_at, @expires_at)
            RETURNING {SelectColumns};
            """);
        command.Parameters.AddWithValue("permission", request.PermissionCode);
        command.Parameters.AddWithValue("grantee", request.GranteeUserId);
        command.Parameters.AddWithValue("delegator", request.DelegatorUserId);
        command.Parameters.AddWithValue("limit", request.LimitAmount);
        command.Parameters.AddWithValue("granted_at", now);
        command.Parameters.AddWithValue("expires_at", request.ExpiresAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Read(reader);
    }

    public async Task<bool> RevokeAsync(
        Guid delegationId, DateTimeOffset at, Guid revokedByUserId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table} SET revoked_at = @at, revoked_by_user_id = @revoked_by
            WHERE delegation_id = @id AND revoked_at IS NULL;
            """);
        command.Parameters.AddWithValue("id", delegationId);
        command.Parameters.AddWithValue("at", at);
        command.Parameters.AddWithValue("revoked_by", revokedByUserId);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AuthorizationDelegation?> FindCoveringAsync(
        Guid granteeUserId,
        string permissionCode,
        decimal amount,
        DateTimeOffset instant,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {SelectColumns}
            FROM {Table}
            WHERE grantee_user_id = @grantee
              AND permission_code = @permission
              AND revoked_at IS NULL
              AND expires_at > @instant
              AND limit_amount >= @amount
            ORDER BY granted_at DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("grantee", granteeUserId);
        command.Parameters.AddWithValue("permission", permissionCode);
        command.Parameters.AddWithValue("instant", instant);
        command.Parameters.AddWithValue("amount", amount);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<AuthorizationDelegation>> ListActiveAsync(
        DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        var result = new List<AuthorizationDelegation>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {SelectColumns} FROM {Table}
            WHERE revoked_at IS NULL AND expires_at > @instant
            ORDER BY expires_at
            LIMIT {MaxActiveRows + 1};
            """);
        command.Parameters.AddWithValue("instant", instant);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Read(reader));

        if (result.Count > MaxActiveRows)
            throw new InvalidOperationException(
                $"{Table} has more than {MaxActiveRows} active rows; ListActiveAsync must be paginated.");

        return result;
    }

    private static AuthorizationDelegation Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetGuid(2),
        reader.GetGuid(3),
        reader.GetDecimal(4),
        reader.GetFieldValue<DateTimeOffset>(5),
        reader.GetFieldValue<DateTimeOffset>(6),
        reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetGuid(8));
}
