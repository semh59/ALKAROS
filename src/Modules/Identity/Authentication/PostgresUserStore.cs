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
                   failed_login_attempts, locked_until, last_login_at,
                   pin_hash, pin_failed_attempts, pin_locked_until
            FROM {Table}
            WHERE username = @username;
            """);
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadUser(reader);
    }

    /// <summary>V1-RMD-151: the unlock path knows the user id from the session.</summary>
    public async Task<StoredUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT user_id, username, password_hash, display_name, active,
                   failed_login_attempts, locked_until, last_login_at,
                   pin_hash, pin_failed_attempts, pin_locked_until
            FROM {Table}
            WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }

    private static StoredUser ReadUser(Npgsql.NpgsqlDataReader reader)
        => new(
            UserId: reader.GetGuid(0),
            Username: reader.GetString(1),
            PasswordHash: reader.GetString(2),
            DisplayName: reader.GetString(3),
            Active: reader.GetBoolean(4),
            FailedLoginAttempts: reader.GetInt32(5),
            LockedUntil: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            LastLoginAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            PinHash: reader.IsDBNull(8) ? null : reader.GetString(8),
            PinFailedAttempts: reader.GetInt32(9),
            PinLockedUntil: reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10));

    /// <summary>
    /// V1-RMD-151: clearing the PIN also clears its counters, so removing and
    /// re-adding one never resumes from a locked state.
    /// </summary>
    public async Task<bool> SetPinAsync(Guid userId, string? encodedPinHash, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET pin_hash = @pin_hash,
                pin_failed_attempts = 0,
                pin_locked_until = NULL,
                updated_at = now(),
                row_version = row_version + 1
            WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("pin_hash", (object?)encodedPinHash ?? DBNull.Value);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    /// <summary>
    /// V1-RMD-151: the PIN's own counters, shaped exactly like
    /// <see cref="RecordLoginFailureAsync"/> — including the "a lock that has
    /// expired restarts the count at 1" branch — but touching only the pin_*
    /// columns so a PIN lock never reaches the password lockout.
    /// </summary>
    public async Task<LoginFailureUpdate?> RecordPinFailureAsync(
        Guid userId,
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET pin_failed_attempts = CASE
                    WHEN pin_locked_until IS NOT NULL AND pin_locked_until <= @now THEN 1
                    ELSE pin_failed_attempts + 1
                END,
                pin_locked_until = CASE
                    WHEN CASE
                        WHEN pin_locked_until IS NOT NULL AND pin_locked_until <= @now THEN 1
                        ELSE pin_failed_attempts + 1
                    END >= @max_failed_attempts
                        THEN @now + @lockout_duration
                    ELSE NULL
                END,
                updated_at = @now,
                row_version = row_version + 1
            WHERE user_id = @user_id
              AND (pin_locked_until IS NULL OR pin_locked_until <= @now)
            RETURNING pin_failed_attempts, pin_locked_until;
            """);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("max_failed_attempts", maxFailedAttempts);
        command.Parameters.AddWithValue("lockout_duration", lockoutDuration);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new LoginFailureUpdate(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task<bool> RecordPinSuccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET pin_failed_attempts = 0,
                pin_locked_until = NULL,
                updated_at = now(),
                row_version = row_version + 1
            WHERE user_id = @user_id
              AND (pin_locked_until IS NULL OR pin_locked_until <= now());
            """);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<LoginFailureUpdate?> RecordLoginFailureAsync(
        Guid userId,
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET failed_login_attempts = CASE
                    WHEN locked_until IS NOT NULL AND locked_until <= @now THEN 1
                    ELSE failed_login_attempts + 1
                END,
                locked_until = CASE
                    WHEN CASE
                        WHEN locked_until IS NOT NULL AND locked_until <= @now THEN 1
                        ELSE failed_login_attempts + 1
                    END >= @max_failed_attempts
                        THEN @now + @lockout_duration
                    ELSE NULL
                END,
                updated_at = @now,
                row_version = row_version + 1
            WHERE user_id = @user_id
              AND (locked_until IS NULL OR locked_until <= @now)
            RETURNING failed_login_attempts, locked_until;
            """);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("max_failed_attempts", maxFailedAttempts);
        command.Parameters.AddWithValue("lockout_duration", lockoutDuration);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new LoginFailureUpdate(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task<bool> RecordLoginSuccessAsync(
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
            WHERE user_id = @user_id
              AND (locked_until IS NULL OR locked_until <= @last_login_at);
            """);
        command.Parameters.AddWithValue("last_login_at", lastLoginAt);
        command.Parameters.AddWithValue("user_id", userId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected == 1;
    }

    /// <summary>
    /// V15-SEC-002: unlike <see cref="RecordLoginSuccessAsync"/>, this clears
    /// the lock unconditionally — that is the entire point of an
    /// administrative force-unlock, so there is no "already unlocked" guard
    /// clause to fail against.
    /// </summary>
    public async Task<bool> ForceUnlockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET failed_login_attempts = 0,
                locked_until = NULL,
                updated_at = now(),
                row_version = row_version + 1
            WHERE user_id = @user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected == 1;
    }

    public async Task<bool> TryUpgradePasswordHashAsync(
        Guid userId,
        string expectedCurrentHash,
        string upgradedHash,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Table}
            SET password_hash = @upgraded_hash,
                updated_at = now(),
                row_version = row_version + 1
            WHERE user_id = @user_id AND password_hash = @expected_hash;
            """);
        command.Parameters.AddWithValue("upgraded_hash", upgradedHash);
        command.Parameters.AddWithValue("expected_hash", expectedCurrentHash);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
