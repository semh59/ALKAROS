using ALKAROS.QrOrdering.TokenLifecycle;
using ALKAROS.TestHelpers;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.QrOrdering.Tests;

/// <summary>
/// Isolated database with catalog, tables and the QR token/session schema —
/// everything the relay-facing session-issue and menu-read endpoints touch.
/// No orders schema: this test project's scope stops at V12-CWB-001's own
/// (order submission is V12-CWB-002's later addition to the same route
/// group).
/// </summary>
public sealed class QrOrderingTestDatabase : PgTestDatabase
{
    public QrOrderingTestDatabase()
        : base("alkaros_qr_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a zone and a table with the given initial status and returns the table id.</summary>
    public async Task<Guid> SeedTableAsync(string status = "Available")
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, @status);
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")[..8]),
            ("table_id", tableId),
            ("table_number", "T-" + tableId.ToString("N")[..6]),
            ("status", status));

        return tableId;
    }

    /// <summary>Seeds one purchasable catalog product and returns its id.</summary>
    public async Task<Guid> SeedProductAsync(string name, decimal price, bool isAvailable = true)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
            VALUES (@product_id, @sku, @name, 1, 1, true, @is_available, @price);
            """,
            ("product_id", productId),
            ("sku", "qr-" + productId.ToString("N")[..8]),
            ("name", name),
            ("is_available", isAvailable),
            ("price", price));

        return productId;
    }

    /// <summary>Issues an active table token directly against the schema (bypassing TableTokenService) and returns the raw token.</summary>
    public async Task<string> SeedActiveTableTokenAsync(Guid tableId, TimeSpan? lifetime = null)
    {
        var (raw, hash) = TableTokenGenerator.Create();
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            """
            INSERT INTO qr_ordering.table_tokens (token_id, table_id, token_hash, issued_at, expires_at)
            VALUES (@token_id, @table_id, @token_hash, @issued_at, @expires_at);
            """,
            ("token_id", Guid.NewGuid()),
            ("table_id", tableId),
            ("token_hash", hash),
            ("issued_at", now),
            ("expires_at", now + (lifetime ?? TableTokenService.DefaultLifetime)));

        return raw;
    }

    /// <summary>Same as <see cref="SeedActiveTableTokenAsync"/> but already revoked — REVOKED validation-failure coverage.</summary>
    public async Task<string> SeedRevokedTableTokenAsync(Guid tableId)
    {
        var (raw, hash) = TableTokenGenerator.Create();
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            """
            INSERT INTO qr_ordering.table_tokens (token_id, table_id, token_hash, issued_at, expires_at, revoked_at, revoked_reason)
            VALUES (@token_id, @table_id, @token_hash, @issued_at, @expires_at, @revoked_at, 'test');
            """,
            ("token_id", Guid.NewGuid()),
            ("table_id", tableId),
            ("token_hash", hash),
            ("issued_at", now),
            ("expires_at", now + TableTokenService.DefaultLifetime),
            ("revoked_at", now));

        return raw;
    }

    /// <summary>Same as <see cref="SeedActiveTableTokenAsync"/> but already expired — EXPIRED validation-failure coverage.</summary>
    public async Task<string> SeedExpiredTableTokenAsync(Guid tableId)
    {
        var (raw, hash) = TableTokenGenerator.Create();
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            """
            INSERT INTO qr_ordering.table_tokens (token_id, table_id, token_hash, issued_at, expires_at)
            VALUES (@token_id, @table_id, @token_hash, @issued_at, @expires_at);
            """,
            ("token_id", Guid.NewGuid()),
            ("table_id", tableId),
            ("token_hash", hash),
            ("issued_at", now - TimeSpan.FromHours(5)),
            ("expires_at", now - TimeSpan.FromHours(1)));

        return raw;
    }

    public async Task<long> NonceCountAsync()
        => await ScalarAsync<long>("SELECT count(*) FROM qr_ordering.relay_request_nonces;");
}
