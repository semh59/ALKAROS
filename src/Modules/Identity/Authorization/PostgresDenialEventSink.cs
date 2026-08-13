using Npgsql;

namespace ALKAROS.Identity.Authorization;

public sealed class PostgresDenialEventSink : IDenialEventSink
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDenialEventSink(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task RecordAsync(DenialEvent denialEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(denialEvent);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO identity.denial_events (denial_event_id, user_id, permission_code, reason, occurred_at)
            VALUES (@id, @user_id, @permission_code, @reason, @occurred_at);
            """);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("user_id", (object?)denialEvent.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("permission_code", denialEvent.PermissionCode);
        command.Parameters.AddWithValue("reason", denialEvent.Reason);
        command.Parameters.AddWithValue("occurred_at", denialEvent.OccurredAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}