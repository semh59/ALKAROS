using Npgsql;

namespace ALKAROS.Identity.Authorization.Grants;

public sealed class PostgresAuthorizationGrantRepository : IAuthorizationGrantRepository
{
    private const string Table = "identity.authorization_grants";
    private const int MaxPendingRows = 5000;

    private const string SelectColumns =
        "grant_id, idempotency_key, permission_code, requester_user_id, requester_role_code, " +
        "subject_type, subject_id, subject_serving_user_id, amount, reason_code, requested_at, " +
        "status, policy_path, approver_user_id, resolved_at";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuthorizationGrantRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AuthorizationGrant?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var command = _dataSource.CreateCommand(
            $"SELECT {SelectColumns} FROM {Table} WHERE idempotency_key = @key;");
        command.Parameters.AddWithValue("key", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<AuthorizationGrant?> GetAsync(Guid grantId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"SELECT {SelectColumns} FROM {Table} WHERE grant_id = @id;");
        command.Parameters.AddWithValue("id", grantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<AuthorizationGrant> InsertAsync(
        AuthorizationGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Table}
                (idempotency_key, permission_code, requester_user_id, requester_role_code,
                 subject_type, subject_id, subject_serving_user_id, amount, reason_code,
                 status, policy_path, approver_user_id, resolved_at)
            VALUES
                (@key, @permission, @requester, @role, @subject_type, @subject_id,
                 @subject_serving, @amount, @reason, @status, @path, @approver, @resolved_at)
            RETURNING {SelectColumns};
            """);
        command.Parameters.AddWithValue("key", grant.IdempotencyKey);
        command.Parameters.AddWithValue("permission", grant.PermissionCode);
        command.Parameters.AddWithValue("requester", grant.RequesterUserId);
        command.Parameters.AddWithValue("role", grant.RequesterRoleCode);
        command.Parameters.AddWithValue("subject_type", (object?)grant.SubjectType ?? DBNull.Value);
        command.Parameters.AddWithValue("subject_id", (object?)grant.SubjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("subject_serving", (object?)grant.SubjectServingUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("amount", grant.Amount);
        command.Parameters.AddWithValue("reason", grant.ReasonCode);
        command.Parameters.AddWithValue("status", GrantText.Status(grant.Status));
        command.Parameters.AddWithValue("path",
            grant.Path is { } p ? GrantText.Path(p) : (object)DBNull.Value);
        command.Parameters.AddWithValue("approver", (object?)grant.ApproverUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("resolved_at", (object?)grant.ResolvedAt ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Read(reader);
    }

    public async Task<AuthorizationGrant> ResolveAsync(
        Guid grantId,
        GrantStatus status,
        PolicyPath path,
        Guid? approverUserId,
        CancellationToken cancellationToken = default)
    {
        if (status == GrantStatus.Pending)
            throw new ArgumentException("Resolution status must be terminal.", nameof(status));

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET status = @status,
                policy_path = @path,
                approver_user_id = @approver,
                resolved_at = now()
            WHERE grant_id = @id AND status = 'pending'
            RETURNING {SelectColumns};
            """);
        command.Parameters.AddWithValue("id", grantId);
        command.Parameters.AddWithValue("status", GrantText.Status(status));
        command.Parameters.AddWithValue("path", GrantText.Path(path));
        command.Parameters.AddWithValue("approver", (object?)approverUserId ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new AuthorizationGrantAlreadyResolvedException(grantId);

        return Read(reader);
    }

    public async Task<int> CountAutoGrantsSinceAsync(
        Guid requesterUserId,
        string permissionCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT count(*)
            FROM {Table}
            WHERE requester_user_id = @requester
              AND permission_code = @permission
              AND status = 'granted'
              AND policy_path = 'auto'
              AND resolved_at >= @since;
            """);
        command.Parameters.AddWithValue("requester", requesterUserId);
        command.Parameters.AddWithValue("permission", permissionCode);
        command.Parameters.AddWithValue("since", since);

        return (int)(long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    public async Task<IReadOnlyList<AuthorizationGrant>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<AuthorizationGrant>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {SelectColumns} FROM {Table}
            WHERE status = 'pending'
            ORDER BY requested_at
            LIMIT {MaxPendingRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Read(reader));

        if (result.Count > MaxPendingRows)
            throw new InvalidOperationException(
                $"{Table} has more than {MaxPendingRows} pending rows; ListPendingAsync must be paginated.");

        return result;
    }

    private static AuthorizationGrant Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetGuid(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetGuid(6),
        reader.IsDBNull(7) ? null : reader.GetGuid(7),
        reader.GetDecimal(8),
        reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10),
        GrantText.Status(reader.GetString(11)),
        reader.IsDBNull(12) ? null : GrantText.Path(reader.GetString(12)),
        reader.IsDBNull(13) ? null : reader.GetGuid(13),
        reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14));
}
