using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.RelaySettings.Tests;

public sealed class RelaySettingsTestDatabase : PgTestDatabase
{
    public RelaySettingsTestDatabase()
        : base("alkaros_qrt003http_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<Guid> SeedTableAsync(string tableNumber, string? zoneName = null)
    {
        var tableId = Guid.NewGuid();
        Guid? zoneId = null;
        if (zoneName is not null)
        {
            zoneId = Guid.NewGuid();
            await ExecuteAsync(
                "INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @code, @name);",
                ("zone_id", zoneId.Value),
                ("code", "Z-" + zoneId.Value.ToString("N")[..8]),
                ("name", zoneName));
        }

        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, 'Available');
            """,
            ("table_id", tableId),
            ("zone_id", (object?)zoneId ?? DBNull.Value),
            ("table_number", tableNumber));
        return tableId;
    }

    /// <summary>The tunnel's run-token envelope is irrelevant to the address, so a placeholder byte is stored.</summary>
    public Task<int> SeedTunnelAsync(string hostname) =>
        ExecuteAsync(
            """
            INSERT INTO qr_ordering.relay_tunnel (tunnel_key, tunnel_id, hostname, token_envelope, updated_at)
            VALUES ('cloudflare', 'tunnel-1', @hostname, '\x00', now());
            """,
            ("hostname", hostname));

    public Task<int> ActiveTokenCountAsync(Guid tableId) =>
        ScalarAsync<int>($"SELECT count(*)::int FROM qr_ordering.table_tokens WHERE table_id = '{tableId:D}' AND revoked_at IS NULL");

    /// <summary>Seeds a session under the given role, with the given permission codes granted outright, and returns the cookie.</summary>
    public Task<string> SeedCashierSessionWithPermissionsAsync(
        Guid terminalId, string roleCode, params string[] permissionCodes) =>
        SeedSessionAsync("cashier", DualScreenApplication.CashierCookieName, terminalId, roleCode, permissionCodes);

    /// <summary>The management surfaces authenticate the <c>alkaros.manager</c> cookie, whose session is bound to a <c>manager:</c> or <c>supervisor:</c> device.</summary>
    public Task<string> SeedManagementSessionAsync(
        string devicePrefix, Guid terminalId, string roleCode, params string[] permissionCodes) =>
        SeedSessionAsync(devicePrefix, CatalogManagementEndpoints.ManagerCookieName, terminalId, roleCode, permissionCodes);

    private async Task<string> SeedSessionAsync(
        string devicePrefix, string cookieName, Guid terminalId, string roleCode, string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Relay Settings API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "qrt003-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"{devicePrefix}:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Relay Settings API Test Role');",
            ("role_id", roleId),
            ("role_code", roleCode + "-" + suffix));
        await ExecuteAsync(
            "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_id", roleId));

        foreach (var code in permissionCodes)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()),
                ("role_id", roleId),
                ("code", code));
        }

        return $"{cookieName}={raw}";
    }
}
