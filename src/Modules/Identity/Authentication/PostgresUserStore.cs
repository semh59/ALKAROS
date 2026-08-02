using Npgsql;

namespace ALKAROS.Identity.Authentication;

/// <summary>
/// PostgreSQL implementation of <see cref="IUserStore"/> over the
/// <c>identity.users</c> table (migration position 005).
/// </summary>
public sealed class PostgresUserStore : IUserStore
{
    private const string Table = "identity.users";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresUserStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<StoredUser?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT user_id, username, password_hash, display_name, active,
                   failed_login_attempts, locked_until, last_login_at
            FROM {Table}
            WHERE username = @username;
            """);
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new StoredUser(
            UserId: reader.GetGuid(0),
            Username: reader.GetString(1),
            PasswordHash: reader.GetString(2),
            DisplayName: reader.GetString(3),
            Active: reader.GetBoolean(4),
            FailedLoginAttempts: reader.GetInt32(5),
            LockedUntil: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            LastLoginAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }

    public async Task RecordLoginFailureAsync(
        Guid userId,
        int failedLoginAttempts,
        DateTimeOffset? lockedUntil,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET failed_login_attempts = @attempts,
                locked_until = @locked_until,
                updated_at = now()
            WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("attempts", failedLoginAttempts);
        command.Parameters.AddWithValue("locked_until", (object?)lockedUntil ?? DBNull.Value);
        command.Parameters.AddWithValue("user_id", userId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
            throw new InvalidOperationException($"User {userId} no longer exists.");
    }

    public async Task RecordLoginSuccessAsync(
        Guid userId,
        DateTimeOffset lastLoginAt,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET failed_login_attempts = 0,
                locked_until = NULL,
                last_login_at = @last_login_at,
                updated_at = now(),
                row_version = row_version + 1
            WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("last_login_at", lastLoginAt);
        command.Parameters.AddWithValue("user_id", userId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
            throw new InvalidOperationException($"User {userId} no longer exists.");
    }
}
