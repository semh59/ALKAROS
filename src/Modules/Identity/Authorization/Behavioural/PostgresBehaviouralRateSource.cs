using Npgsql;

namespace ALKAROS.Identity.Authorization.Behavioural;

public sealed class PostgresBehaviouralRateSource : IBehaviouralRateSource
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBehaviouralRateSource(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<int> CountGrantedSinceAsync(
        Guid userId,
        string permissionCode,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await using var command = _dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM identity.authorization_grants
            WHERE requester_user_id = @user
              AND permission_code = @permission
              AND status = 'granted'
              AND resolved_at >= @since;
            """);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("permission", permissionCode);
        command.Parameters.AddWithValue("since", since);

        return (int)(long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }
}
