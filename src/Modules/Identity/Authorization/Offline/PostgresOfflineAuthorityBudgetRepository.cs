using Npgsql;

namespace ALKAROS.Identity.Authorization.Offline;

public sealed class PostgresOfflineAuthorityBudgetRepository : IOfflineAuthorityBudgetRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresOfflineAuthorityBudgetRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<OfflineAuthorityBudget> CreateAsync(
        Guid userId,
        Guid sessionId,
        IReadOnlyList<OfflineAuthorityBudgetLine> lines,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (expiresAt <= issuedAt)
            throw new ArgumentException("A budget must expire after it is issued.", nameof(expiresAt));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Re-issuing for a session no longer deletes the prior budget first
        // (migration 050 drops uq_offline_authority_budgets_session): a prior
        // budget can already have offline_authority_replays rows once
        // reconciled, and that FK has no ON DELETE CASCADE, so the delete
        // would throw on exactly the session that most needs a fresh budget.
        // The prior row is left in place, undated; GetBySessionAsync /
        // LoadAsync return the most recently issued one.
        // A fresh login must not hand back a full budget: what this user already spent on budgets that are still valid
        // is taken off the new lines.
        var spent = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (var spentQuery = new NpgsqlCommand(
            """
            SELECT g.permission_code, count(*)
            FROM identity.offline_authority_replays r
            JOIN identity.offline_authority_budgets b ON b.budget_id = r.budget_id
            JOIN identity.authorization_grants g ON g.grant_id = r.grant_id
            WHERE b.user_id = @user AND b.expires_at > @issued_at AND g.status <> 'denied'
            GROUP BY g.permission_code;
            """,
            connection, transaction))
        {
            spentQuery.Parameters.AddWithValue("user", userId);
            spentQuery.Parameters.AddWithValue("issued_at", issuedAt);
            await using var reader = await spentQuery.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                spent[reader.GetString(0)] = (int)reader.GetInt64(1);
        }

        lines = lines
            .Select(line => line with { MaxCount = Math.Max(0, line.MaxCount - spent.GetValueOrDefault(line.PermissionCode)) })
            .ToList();

        Guid budgetId;
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO identity.offline_authority_budgets (user_id, session_id, issued_at, expires_at)
            VALUES (@user, @session, @issued_at, @expires_at)
            RETURNING budget_id;
            """,
            connection, transaction))
        {
            insert.Parameters.AddWithValue("user", userId);
            insert.Parameters.AddWithValue("session", sessionId);
            insert.Parameters.AddWithValue("issued_at", issuedAt);
            insert.Parameters.AddWithValue("expires_at", expiresAt);
            budgetId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        foreach (var line in lines)
        {
            await using var lineInsert = new NpgsqlCommand(
                """
                INSERT INTO identity.offline_authority_budget_lines
                    (budget_id, permission_code, limit_amount, max_count)
                VALUES (@budget, @permission, @limit, @count);
                """,
                connection, transaction);
            lineInsert.Parameters.AddWithValue("budget", budgetId);
            lineInsert.Parameters.AddWithValue("permission", line.PermissionCode);
            lineInsert.Parameters.AddWithValue("limit", (object?)line.LimitAmount ?? DBNull.Value);
            lineInsert.Parameters.AddWithValue("count", line.MaxCount);
            await lineInsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new OfflineAuthorityBudget(budgetId, userId, sessionId, issuedAt, expiresAt, lines);
    }

    public Task<OfflineAuthorityBudget?> GetAsync(
        Guid budgetId, CancellationToken cancellationToken = default)
        => LoadAsync("budget_id = @key", budgetId, cancellationToken);

    public Task<OfflineAuthorityBudget?> GetBySessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
        => LoadAsync("session_id = @key", sessionId, cancellationToken);

    private async Task<OfflineAuthorityBudget?> LoadAsync(
        string whereClause, Guid key, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        Guid budgetId;
        Guid userId;
        Guid sessionId;
        DateTimeOffset issuedAt;
        DateTimeOffset expiresAt;

        await using (var head = new NpgsqlCommand(
            $"""
            SELECT budget_id, user_id, session_id, issued_at, expires_at
            FROM identity.offline_authority_budgets
            WHERE {whereClause}
            ORDER BY issued_at DESC
            LIMIT 1;
            """,
            connection))
        {
            head.Parameters.AddWithValue("key", key);
            await using var reader = await head.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            budgetId = reader.GetGuid(0);
            userId = reader.GetGuid(1);
            sessionId = reader.GetGuid(2);
            issuedAt = reader.GetFieldValue<DateTimeOffset>(3);
            expiresAt = reader.GetFieldValue<DateTimeOffset>(4);
        }

        var lines = new List<OfflineAuthorityBudgetLine>();
        await using (var lineQuery = new NpgsqlCommand(
            """
            SELECT permission_code, limit_amount, max_count
            FROM identity.offline_authority_budget_lines
            WHERE budget_id = @budget
            ORDER BY permission_code;
            """,
            connection))
        {
            lineQuery.Parameters.AddWithValue("budget", budgetId);
            await using var reader = await lineQuery.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new OfflineAuthorityBudgetLine(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                    reader.GetInt32(2)));
            }
        }

        return new OfflineAuthorityBudget(budgetId, userId, sessionId, issuedAt, expiresAt, lines);
    }
}
