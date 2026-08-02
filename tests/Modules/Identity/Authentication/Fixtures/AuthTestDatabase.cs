using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authentication.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-IAM-001 identity.users schema.
/// </summary>
public sealed class AuthTestDatabase : PgTestDatabase
{
    public AuthTestDatabase()
        : base("alkaros_iam001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// Inserts a user row with the given credential state. Returns the new user id.
    /// </summary>
    public async Task<Guid> InsertUserAsync(
        string username,
        string passwordHash,
        string displayName = "Test User",
        bool active = true,
        int failedLoginAttempts = 0,
        DateTimeOffset? lockedUntil = null,
        DateTimeOffset? lastLoginAt = null)
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active,
                 failed_login_attempts, locked_until, last_login_at)
            VALUES
                (@user_id, @username, @password_hash, @display_name, @active,
                 @attempts, @locked_until, @last_login_at);
            """,
            ("user_id", userId),
            ("username", username),
            ("password_hash", passwordHash),
            ("display_name", displayName),
            ("active", active),
            ("attempts", failedLoginAttempts),
            ("locked_until", (object?)lockedUntil ?? DBNull.Value),
            ("last_login_at", (object?)lastLoginAt ?? DBNull.Value));

        return userId;
    }

    /// <summary>
    /// Forces the user's lock window to expire immediately.
    /// </summary>
    public async Task ForceLockExpiredAsync(Guid userId, DateTimeOffset expiredAt)
        => await ExecuteAsync(
            "UPDATE identity.users SET locked_until = @expired_at WHERE user_id = @user_id;",
            ("expired_at", expiredAt),
            ("user_id", userId));
}
