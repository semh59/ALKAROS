using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.PendingOrderNotifications.Tests;

/// <summary>
/// V1-RMD-202: users, roles/permissions and device sessions (what
/// <c>ResolveMostSuitableWaiterAsync</c>'s candidate filter reads) plus
/// <c>orders.orders</c> (its load/rotation ranking) — table_mgmt and
/// catalog exist only because orders.orders/order_items' own FK
/// constraints reference them; no row is ever inserted into either.
/// </summary>
public sealed class PendingOrderNotificationsTestDatabase : PgTestDatabase
{
    private const string OrdersSendPermissionCode = "orders.send";

    private Guid? _waiterRoleId;

    public PendingOrderNotificationsTestDatabase()
        : base("alkaros_pendingorder_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f, StringComparer.Ordinal))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// Seeds an active user holding orders.send, optionally with a live
    /// (unexpired, unrevoked) device session — the exact candidacy gate
    /// ResolveMostSuitableWaiterAsync checks.
    /// </summary>
    public async Task<Guid> SeedWaiterAsync(string displayName, bool hasOpenSession)
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', @display_name, true);
            """,
            ("user_id", userId),
            ("username", "waiter-" + userId.ToString("N")),
            ("display_name", displayName));

        var roleId = await EnsureWaiterRoleAsync();
        await ExecuteAsync(
            "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()), ("user_id", userId), ("role_id", roleId));

        if (hasOpenSession)
        {
            var (raw, hash) = DeviceSessionToken.Create();
            _ = raw;
            await ExecuteAsync(
                """
                INSERT INTO identity.device_sessions
                    (session_id, user_id, device_id, token_hash, created_at, expires_at)
                VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
                """,
                ("session_id", Guid.NewGuid()),
                ("user_id", userId),
                ("device_id", $"waiter-app:{Guid.NewGuid():N}"),
                ("token_hash", hash));
        }

        return userId;
    }

    /// <summary>Seeds one order assigned to <paramref name="servingUserId"/> for load/rotation ranking.</summary>
    public Task SeedOrderAsync(Guid servingUserId, string status, DateTimeOffset createdAt)
        => SeedOrderAsync(servingUserId, status, createdAt, tableId: null);

    /// <summary>V1-RMD-208: same, but attributed to a real table for zone-preference ranking.</summary>
    public async Task SeedOrderAsync(Guid servingUserId, string status, DateTimeOffset createdAt, Guid? tableId)
    {
        await ExecuteAsync(
            """
            INSERT INTO orders.orders
                (order_id, source, table_id, status, confirmation_status, order_number,
                 serving_user_id, created_at, updated_at)
            VALUES
                (@order_id, 'Waiter', @table_id, @status, 'NotRequired', @order_number,
                 @serving_user_id, @created_at, @created_at);
            """,
            ("order_id", Guid.NewGuid()),
            ("table_id", (object?)tableId ?? DBNull.Value),
            ("status", status),
            ("order_number", "ORD-" + Guid.NewGuid().ToString("N")[..12]),
            ("serving_user_id", servingUserId),
            ("created_at", createdAt));
    }

    /// <summary>V1-RMD-208: a zone, for the "same zone as this table" preference.</summary>
    public async Task<Guid> SeedZoneAsync(string name)
    {
        var zoneId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @code, @name);",
            ("zone_id", zoneId), ("code", "zone-" + zoneId.ToString("N")[..12]), ("name", name));
        return zoneId;
    }

    /// <summary>V1-RMD-208: a real table in <paramref name="zoneId"/>, for zone-preference ranking.</summary>
    public async Task<Guid> SeedTableAsync(Guid zoneId, string tableNumber)
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, current_status)
            VALUES (@table_id, @zone_id, @table_number, 'Available');
            """,
            ("table_id", tableId), ("zone_id", zoneId), ("table_number", tableNumber));
        return tableId;
    }

    private async Task<Guid> EnsureWaiterRoleAsync()
    {
        if (_waiterRoleId is Guid existing) return existing;

        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, 'test-waiter', 'Test Waiter');",
            ("role_id", roleId));
        await ExecuteAsync(
            "INSERT INTO identity.permissions (permission_id, code, name) VALUES (@permission_id, @code, 'Send orders');",
            ("permission_id", permissionId), ("code", OrdersSendPermissionCode));
        await ExecuteAsync(
            """
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            VALUES (@id, @role_id, @permission_id);
            """,
            ("id", Guid.NewGuid()), ("role_id", roleId), ("permission_id", permissionId));

        _waiterRoleId = roleId;
        return roleId;
    }
}
