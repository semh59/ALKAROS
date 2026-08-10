using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.DeviceSessions.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-IAM-003 device session schemas.
/// </summary>
public sealed class DeviceSessionsTestDatabase : PgTestDatabase
{
    public DeviceSessionsTestDatabase()
        : base("alkaros_iam003_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<Guid> InsertUserAsync()
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active)
            VALUES
                (@user_id, @username, @password_hash, @display_name, @active);
            """,
            ("user_id", userId),
            ("username", "owner_" + Guid.NewGuid().ToString("N")[..20]),
            ("password_hash", "pbkdf2-sha256$600000$not-used-in-tests"),
            ("display_name", "Test Owner"),
            ("active", true));

        return userId;
    }
}