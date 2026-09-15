using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.WaiterNotifications.Tests;

/// <summary>
/// V1-RMD-201: users and device sessions — everything
/// <see cref="DualScreenStore.AuthenticateCashierAsync"/> touches, same
/// fixture shape as <c>WebPushTestDatabase</c> in the WebPush test project.
/// </summary>
public sealed class WaiterNotificationsTestDatabase : PgTestDatabase
{
    public WaiterNotificationsTestDatabase()
        : base("alkaros_waiternotif_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f, StringComparer.Ordinal))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a cashier device session for this terminal id, returning its user id and cookie.</summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionAsync(Guid terminalId)
    {
        var userId = Guid.NewGuid();
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Waiter Notifications Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "waiter-notif-" + userId.ToString("N")),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }
}
