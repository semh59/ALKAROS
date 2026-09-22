using ALKAROS.TestHelpers;

namespace ALKAROS.Security.IdentityHardening.Tests.Fixtures;

/// <summary>
/// Real Postgres fixture combining V1-IAM-001 (users) and V1-IAM-003
/// (device_sessions) — V15-SEC-002 composes with both.
/// </summary>
public sealed class IdentityHardeningTestDatabase : PgTestDatabase
{
    public IdentityHardeningTestDatabase()
        : base("alkaros_sec002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<Guid> InsertUserAsync(
        int failedLoginAttempts = 0,
        DateTimeOffset? lockedUntil = null,
        bool active = true)
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active,
                 failed_login_attempts, locked_until)
            VALUES
                (@user_id, @username, @password_hash, @display_name, @active,
                 @failed_login_attempts, @locked_until);
            """,
            ("user_id", userId),
            ("username", "user_" + userId.ToString("N")[..20]),
            ("password_hash", "pbkdf2-sha256$600000$not-used-in-tests"),
            ("display_name", "Test User"),
            ("active", active),
            ("failed_login_attempts", failedLoginAttempts),
            ("locked_until", (object?)lockedUntil ?? DBNull.Value));

        return userId;
    }
}
