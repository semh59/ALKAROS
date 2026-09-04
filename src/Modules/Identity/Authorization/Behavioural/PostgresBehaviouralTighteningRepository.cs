using Npgsql;

namespace ALKAROS.Identity.Authorization.Behavioural;

public sealed class PostgresBehaviouralTighteningRepository : IBehaviouralTighteningRepository
{
    private const string UniqueViolation = "23505";
    private const string Table = "identity.behavioural_tightenings";
    private const int MaxActiveRows = 5000;

    private const string SelectColumns =
        "tightening_id, user_id, permission_code, recent_count, baseline_per_window, " +
        "trigger_ratio, triggered_at, cleared_at, cleared_by_user_id";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBehaviouralTighteningRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<BehaviouralTightening?> FindActiveAsync(
        Guid userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {SelectColumns} FROM {Table}
            WHERE user_id = @user AND permission_code = @permission AND cleared_at IS NULL;
            """);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("permission", permissionCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<BehaviouralTightening> OpenAsync(
        BehaviouralTightening draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        try
        {
            await using var command = _dataSource.CreateCommand(
                $"""
                INSERT INTO {Table}
                    (user_id, permission_code, recent_count, baseline_per_window,
                     trigger_ratio, triggered_at)
                VALUES (@user, @permission, @recent, @baseline, @ratio, @triggered_at)
                RETURNING {SelectColumns};
                """);
            command.Parameters.AddWithValue("user", draft.UserId);
            command.Parameters.AddWithValue("permission", draft.PermissionCode);
            command.Parameters.AddWithValue("recent", draft.RecentCount);
            command.Parameters.AddWithValue("baseline", draft.BaselinePerWindow);
            command.Parameters.AddWithValue("ratio", draft.TriggerRatio);
            command.Parameters.AddWithValue("triggered_at", draft.TriggeredAt);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return Read(reader);
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            var winner = await FindActiveAsync(draft.UserId, draft.PermissionCode, cancellationToken);
            if (winner is null)
                throw;
            return winner;
        }
    }

    public async Task<BehaviouralTightening> ClearAsync(
        Guid tighteningId, Guid clearedByUserId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET cleared_at = @at, cleared_by_user_id = @by
            WHERE tightening_id = @id AND cleared_at IS NULL
            RETURNING {SelectColumns};
            """);
        command.Parameters.AddWithValue("id", tighteningId);
        command.Parameters.AddWithValue("at", at);
        command.Parameters.AddWithValue("by", clearedByUserId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new BehaviouralTighteningAlreadyClearedException(tighteningId);

        return Read(reader);
    }

    public async Task<DateTimeOffset?> MostRecentClearAsync(
        Guid userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT max(cleared_at) FROM {Table}
            WHERE user_id = @user AND permission_code = @permission AND cleared_at IS NOT NULL;
            """);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("permission", permissionCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0)
            ? reader.GetFieldValue<DateTimeOffset>(0)
            : null;
    }

    public async Task<IReadOnlyList<BehaviouralTightening>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<BehaviouralTightening>();

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {SelectColumns} FROM {Table}
            WHERE cleared_at IS NULL
            ORDER BY triggered_at
            LIMIT {MaxActiveRows + 1};
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Read(reader));

        if (result.Count > MaxActiveRows)
            throw new InvalidOperationException(
                $"{Table} has more than {MaxActiveRows} open rows; ListActiveAsync must be paginated.");

        return result;
    }

    private static BehaviouralTightening Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.GetInt32(3),
        reader.GetDecimal(4),
        reader.GetDecimal(5),
        reader.GetFieldValue<DateTimeOffset>(6),
        reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetGuid(8));
}
