using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.KitchenOperations.Tests;

public sealed record KitchenSeed(Guid OrderId, Guid TicketId, Guid ItemId, Guid PrinterId, Guid RouteId, Guid PrintJobId, Guid DeliveryId);

public sealed class KitchenOperationsTestDatabase : PgTestDatabase
{
    public KitchenOperationsTestDatabase() : base("alkaros_rmd015_") { }

    protected override async Task ApplySqlAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Migration ID is missing.");
            var files = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories);
            if (files.Length != 1)
                throw new InvalidOperationException($"Expected one migration script for {id}, found {files.Length}.");
            await RunAsync(DataSource, await File.ReadAllTextAsync(files[0]));
        }
    }

    public async Task<string> SeedSessionAsync(
        Guid terminalId,
        IReadOnlyList<string> permissions,
        bool expired = false)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var (rawToken, tokenHash) = DeviceSessionToken.Create();
        await RunAsync(
            DataSource,
            $$"""
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES ('{{userId:D}}', 'kitchen-{{userId:N}}', 'not-used', 'Kitchen API Test', TRUE);

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{userId:D}}', 'cashier:{{terminalId:D}}', '{{tokenHash}}', now(),
                 CASE WHEN {{expired.ToString().ToLowerInvariant()}} THEN now() - interval '1 minute'
                      ELSE now() + interval '1 hour' END);
            """);

        if (permissions.Count > 0)
        {
            await RunAsync(
                DataSource,
                $$"""
                INSERT INTO identity.roles (role_id, code, name)
                VALUES ('{{roleId:D}}', 'kitchen-role-{{roleId:N}}', 'Kitchen API Test Role');
                """);
            foreach (var permission in permissions)
            {
                var permissionId = Guid.NewGuid();
                await RunAsync(
                    DataSource,
                    $$"""
                    INSERT INTO identity.permissions (permission_id, code, name)
                    VALUES ('{{permissionId:D}}', '{{permission}}', '{{permission}}')
                    ON CONFLICT (code) DO NOTHING;

                    INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                    SELECT '{{Guid.NewGuid():D}}', '{{roleId:D}}', permission_id
                    FROM identity.permissions WHERE code = '{{permission}}';
                    """);
            }

            await RunAsync(
                DataSource,
                $$"""
                INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
                VALUES ('{{Guid.NewGuid():D}}', '{{userId:D}}', '{{roleId:D}}');
                """);
        }

        return $"alkaros.cashier={rawToken}";
    }

    public async Task<KitchenSeed> SeedKitchenGraphAsync()
    {
        var orderId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        await RunAsync(
            DataSource,
            $$"""
            INSERT INTO orders.orders (
                order_id, source, status, confirmation_status, order_number, created_at, updated_at)
            VALUES ('{{orderId:D}}', 'Cashier', 'Submitted', 'NotRequired', 'ORD-{{orderId:N}}', now(), now());

            INSERT INTO kitchen.kitchen_tickets (
                id, order_id, ticket_number, station_id, status, row_version, created_at)
            VALUES ('{{ticketId:D}}', '{{orderId:D}}', 'KT-{{ticketId:N}}', 'hot-line', 'Queued', 1, now());

            INSERT INTO kitchen.kitchen_ticket_items (
                id, ticket_id, order_item_id, product_id, product_name_snapshot, quantity,
                modifiers_summary, notes, status, row_version, created_at)
            VALUES ('{{itemId:D}}', '{{ticketId:D}}', '{{Guid.NewGuid():D}}', '{{Guid.NewGuid():D}}',
                    'Test soup', 2, 'Extra herbs', 'az tuz, acisiz', 'Queued', 1, now());

            INSERT INTO kitchen.printers (id, name, station_id, ip_address, port, is_active, created_at)
            VALUES ('{{printerId:D}}', 'Hot line printer', 'hot-line', '10.0.0.8', 9100, TRUE, now());

            INSERT INTO kitchen.printer_routes (id, route_level, printer_id, is_active, created_at)
            VALUES ('{{routeId:D}}', 'Default', '{{printerId:D}}', TRUE, now());

            INSERT INTO kitchen.print_jobs (
                id, ticket_id, printer_id, idempotency_key, payload, status, attempt_count,
                max_attempts, row_version, created_at)
            VALUES ('{{printJobId:D}}', '{{ticketId:D}}', '{{printerId:D}}', 'test-{{printJobId:N}}',
                    'secret-payload-must-not-leave-DTO', 'Pending', 0, 5, 1, now());

            INSERT INTO kitchen.physical_print_deliveries (
                id, print_job_id, ticket_id, printer_id, status, attempt_number, is_reprint,
                crash_window_reason, payload_snapshot, created_at, row_version, state_changed_at)
            VALUES ('{{deliveryId:D}}', '{{printJobId:D}}', '{{ticketId:D}}', '{{printerId:D}}', 'Unknown', 1,
                    FALSE, 'Socket acknowledgement was lost', 'secret-payload-must-not-leave-DTO', now(), 1, now());

            INSERT INTO operations.backups (
                backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status,
                error_message, started_at, completed_at, retention_days)
            VALUES ('{{Guid.NewGuid():D}}', 'Full', '/srv/private/backup.bak', 0, 'not-a-checksum', 'Failed',
                    'Backup engine unavailable', now(), now(), 30);

            INSERT INTO operations.system_health_snapshots (
                snapshot_id, database_status, disk_status, last_backup_status,
                free_disk_bytes, database_size_bytes, captured_at)
            VALUES ('{{Guid.NewGuid():D}}', 'Healthy', 'Unhealthy', 'Unhealthy', 10, 100, now());

            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_type, reason,
                correlation_id, before_state_json, after_state_json, metadata_json)
            VALUES ('{{Guid.NewGuid():D}}', 'KitchenTicketTransitioned', 'KitchenTicket', '{{ticketId:D}}',
                    'User', 'Started preparing', 'corr-{{ticketId:N}}',
                    jsonb_build_object('token', 'secret'),
                    jsonb_build_object('status', 'Preparing'),
                    jsonb_build_object('internal', 'hidden'));
            """);

        return new KitchenSeed(orderId, ticketId, itemId, printerId, routeId, printJobId, deliveryId);
    }

    /// <summary>
    /// V1-RMD-130: an order + kitchen ticket + active printer at the same
    /// station, deliberately with NO print_jobs row yet — the exact scenario
    /// <c>KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync</c>
    /// must detect and act on. Distinct from <see cref="SeedKitchenGraphAsync"/>,
    /// which always seeds a pre-existing print job for its ticket.
    /// </summary>
    public async Task<(Guid TicketId, Guid PrinterId, string StationId)> SeedTicketAwaitingPrintJobAsync(
        Guid? tableId = null, string? tableNumber = null)
    {
        var orderId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var stationId = "bridge-station-" + ticketId.ToString("N")[..8];

        if (tableId is { } id)
        {
            await RunAsync(
                DataSource,
                $$"""
                INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
                VALUES ('{{id:D}}', '{{tableNumber}}', 4, TRUE, 'Occupied');
                """);
        }

        await RunAsync(
            DataSource,
            $$"""
            INSERT INTO orders.orders (
                order_id, source, table_id, status, confirmation_status, order_number, created_at, updated_at)
            VALUES ('{{orderId:D}}', 'Cashier', {{(tableId is { } t ? $"'{t:D}'" : "NULL")}}, 'Submitted',
                    'NotRequired', 'ORD-{{orderId:N}}', now(), now());

            INSERT INTO kitchen.kitchen_tickets (
                id, order_id, ticket_number, station_id, status, row_version, created_at)
            VALUES ('{{ticketId:D}}', '{{orderId:D}}', 'KT-{{ticketId:N}}', '{{stationId}}', 'Queued', 1, now());

            INSERT INTO kitchen.kitchen_ticket_items (
                id, ticket_id, order_item_id, product_id, product_name_snapshot, quantity,
                status, row_version, created_at)
            VALUES ('{{Guid.NewGuid():D}}', '{{ticketId:D}}', '{{Guid.NewGuid():D}}', '{{Guid.NewGuid():D}}',
                    'Bridge test item', 1, 'Queued', 1, now());

            INSERT INTO kitchen.printers (id, name, station_id, ip_address, port, is_active, created_at)
            VALUES ('{{printerId:D}}', 'Bridge test printer {{printerId:N}}', '{{stationId}}',
                    '10.0.0.9', 9100, TRUE, now());
            """);

        return (ticketId, printerId, stationId);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
