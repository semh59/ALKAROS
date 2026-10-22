using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Secrets;
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

    /// <summary>A store whose envelope master key is a fresh random one; pass the same provider to model a restart.</summary>
    public PushSubscriptionStore NewStore(ISecretProvider? secrets = null)
        => new(DataSource, secrets ?? NewSecrets());

    public static InMemorySecretProvider NewSecrets()
    {
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        return secrets;
    }

    public Task<int> DeactivateUserAsync(Guid userId)
        => ExecuteAsync("UPDATE identity.users SET active = false WHERE user_id = @user_id;", ("user_id", userId));

    public Task<int> FailureCountAsync(string endpoint)
        => ScalarAsync<int>($"SELECT failure_count FROM notifications.push_subscriptions WHERE endpoint = '{endpoint}';");

    public Task<Guid> SubscriptionOwnerAsync(string endpoint)
        => ScalarAsync<Guid>($"SELECT user_id FROM notifications.push_subscriptions WHERE endpoint = '{endpoint}';");

    public Task<bool> VapidPrivateKeyIsPlaintextAsync()
        => ScalarAsync<bool>("SELECT private_key IS NOT NULL FROM notifications.vapid_keys;");

    public Task<bool> VapidEnvelopeExistsAsync()
        => ScalarAsync<bool>("SELECT private_key_envelope IS NOT NULL FROM notifications.vapid_keys;");

    public Task<int> SeedPlaintextVapidAsync(string publicKey, string privateKey)
        => ExecuteAsync(
            "INSERT INTO notifications.vapid_keys (single_row, public_key, private_key, subject) VALUES (TRUE, @public_key, @private_key, 'mailto:old@alkaros.local');",
            ("public_key", publicKey), ("private_key", privateKey));

    public Task<long> SubscriptionCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM notifications.push_subscriptions;");

    public Task<string> StoredKeyAsync(string endpoint)
        => ScalarAsync<string>(
            $"SELECT p256dh FROM notifications.push_subscriptions WHERE endpoint = '{endpoint}';");

    public Task<long> VapidRowCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM notifications.vapid_keys;");

    /// <summary>
    /// V1-RMD-201: a bare user row for tests that need a real
    /// <c>identity.users</c> FK for <c>push_subscriptions.user_id</c> but no
    /// device session (unlike <see cref="SeedCashierSessionAsync"/>).
    /// </summary>
    public async Task<Guid> SeedUserAsync(string displayName)
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', @display_name, true);
            """,
            ("user_id", userId),
            ("username", "push-user-" + userId.ToString("N")),
            ("display_name", displayName));
        return userId;
    }
}
