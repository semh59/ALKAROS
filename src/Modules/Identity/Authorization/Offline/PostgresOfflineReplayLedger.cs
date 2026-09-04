using ALKAROS.Identity.Authorization.Grants;
using Npgsql;

namespace ALKAROS.Identity.Authorization.Offline;

public sealed class PostgresOfflineReplayLedger : IOfflineReplayLedger
{
    private const string GrantColumns =
        "grant_id, idempotency_key, permission_code, requester_user_id, requester_role_code, " +
        "subject_type, subject_id, subject_serving_user_id, amount, reason_code, requested_at, " +
        "status, policy_path, approver_user_id, resolved_at";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresOfflineReplayLedger(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AuthorizationGrant> RecordAsync(
        AuthorizationGrant draftGrant,
        Guid budgetId,
        DateTimeOffset offlineAuthorizedAt,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draftGrant);
        if (offlineAuthorizedAt > reconciledAt)
            throw new ArgumentException(
                "An offline action cannot be authorized after it was reconciled.", nameof(offlineAuthorizedAt));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        AuthorizationGrant stored;
        await using (var grantInsert = new NpgsqlCommand(
            $"""
            INSERT INTO identity.authorization_grants
                (idempotency_key, permission_code, requester_user_id, requester_role_code,
                 subject_type, subject_id, subject_serving_user_id, amount, reason_code,
                 requested_at, status, policy_path, approver_user_id, resolved_at)
            VALUES
                (@key, @permission, @requester, @role, @subject_type, @subject_id,
                 @subject_serving, @amount, @reason, @requested_at, @status, @path, @approver, @resolved_at)
            RETURNING {GrantColumns};
            """,
            connection, transaction))
        {
            grantInsert.Parameters.AddWithValue("key", draftGrant.IdempotencyKey);
            grantInsert.Parameters.AddWithValue("permission", draftGrant.PermissionCode);
            grantInsert.Parameters.AddWithValue("requester", draftGrant.RequesterUserId);
            grantInsert.Parameters.AddWithValue("role", draftGrant.RequesterRoleCode);
            grantInsert.Parameters.AddWithValue("subject_type", (object?)draftGrant.SubjectType ?? DBNull.Value);
            grantInsert.Parameters.AddWithValue("subject_id", (object?)draftGrant.SubjectId ?? DBNull.Value);
            grantInsert.Parameters.AddWithValue(
                "subject_serving", (object?)draftGrant.SubjectServingUserId ?? DBNull.Value);
            grantInsert.Parameters.AddWithValue("amount", draftGrant.Amount);
            grantInsert.Parameters.AddWithValue("reason", draftGrant.ReasonCode);
            grantInsert.Parameters.AddWithValue("requested_at", reconciledAt);
            grantInsert.Parameters.AddWithValue("status", GrantText.Status(draftGrant.Status));
            grantInsert.Parameters.AddWithValue(
                "path", draftGrant.Path is { } p ? GrantText.Path(p) : (object)DBNull.Value);
            grantInsert.Parameters.AddWithValue("approver", (object?)draftGrant.ApproverUserId ?? DBNull.Value);
            grantInsert.Parameters.AddWithValue("resolved_at", (object?)draftGrant.ResolvedAt ?? DBNull.Value);

            await using var reader = await grantInsert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            stored = Read(reader);
        }

        await using (var replayInsert = new NpgsqlCommand(
            """
            INSERT INTO identity.offline_authority_replays
                (grant_id, budget_id, offline_authorized_at, reconciled_at)
            VALUES (@grant, @budget, @offline_at, @reconciled_at);
            """,
            connection, transaction))
        {
            replayInsert.Parameters.AddWithValue("grant", stored.GrantId);
            replayInsert.Parameters.AddWithValue("budget", budgetId);
            replayInsert.Parameters.AddWithValue("offline_at", offlineAuthorizedAt);
            replayInsert.Parameters.AddWithValue("reconciled_at", reconciledAt);
            await replayInsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    public async Task<int> CountReconciledAsync(
        Guid budgetId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await using var command = _dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM identity.offline_authority_replays r
            JOIN identity.authorization_grants g ON g.grant_id = r.grant_id
            WHERE r.budget_id = @budget
              AND g.permission_code = @permission
              AND g.status <> 'denied';
            """);
        command.Parameters.AddWithValue("budget", budgetId);
        command.Parameters.AddWithValue("permission", permissionCode);

        return (int)(long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
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
