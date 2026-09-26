using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.HelpRequests.Tests;

/// <summary>
/// V1-WTR-014: users, device sessions, a table to raise a request against,
/// and the help-requests table itself - everything the endpoint touches
/// and nothing else.
/// </summary>
public sealed class HelpRequestTestDatabase : PgTestDatabase
{
    public HelpRequestTestDatabase()
        : base("alkaros_help_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f, StringComparer.Ordinal))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a cashier device session for this terminal id.</summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionAsync(Guid terminalId)
    {
        var userId = Guid.NewGuid();
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Help Request API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "help-api-" + userId.ToString("N")),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>Seeds a zone and a table, returns (tableId, tableNumber).</summary>
    public async Task<(Guid TableId, string TableNumber)> SeedTableAsync()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var tableNumber = "T-" + tableId.ToString("N")[..6];
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, 'Available');
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")[..8]),
            ("table_id", tableId),
            ("table_number", tableNumber));

        return (tableId, tableNumber);
    }

    public Task<long> HelpRequestCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM notifications.help_requests;");

    /// <summary>V1-RMD-289: seeds a manager or supervisor device session - the cookie HelpRequestHub itself
    /// authenticates (`alkaros.manager`), never the cashier one <see cref="SeedCashierSessionAsync"/> seeds.</summary>
    public async Task<(Guid UserId, string RawToken)> SeedManagementSessionAsync(string devicePrefix)
    {
        var userId = Guid.NewGuid();
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Help Request Hub Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "help-hub-" + userId.ToString("N")),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"{devicePrefix}:{Guid.NewGuid():D}"),
            ("token_hash", hash));

        return (userId, raw);
    }
}
