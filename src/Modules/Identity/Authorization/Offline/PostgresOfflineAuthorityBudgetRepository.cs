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

        await using (var delete = new NpgsqlCommand(
            "DELETE FROM identity.offline_authority_budgets WHERE session_id = @session;",
            connection, transaction))
        {
            delete.Parameters.AddWithValue("session", sessionId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

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
            WHERE {whereClause};
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
