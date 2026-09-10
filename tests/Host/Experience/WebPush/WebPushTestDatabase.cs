using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.WebPush.Tests;

/// <summary>
/// V1-WTR-011: users, device sessions and the push tables — everything the
/// subscription endpoints touch and nothing else.
/// </summary>
public sealed class WebPushTestDatabase : PgTestDatabase
{
    public WebPushTestDatabase()
        : base("alkaros_push_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f, StringComparer.Ordinal))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a cashier device session for this terminal id.</summary>
    public async Task<string> SeedCashierSessionAsync(Guid terminalId)
    {
        var userId = Guid.NewGuid();
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Web Push API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "push-api-" + userId.ToString("N")),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    public Task<long> SubscriptionCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM notifications.push_subscriptions;");

    public Task<string> StoredKeyAsync(string endpoint)
        => ScalarAsync<string>(
            $"SELECT p256dh FROM notifications.push_subscriptions WHERE endpoint = '{endpoint}';");

    public Task<long> VapidRowCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM notifications.vapid_keys;");
}
