using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Tables;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Tables.Reservations;
using ALKAROS.Tables.TableMerge;
using ALKAROS.Tables.TableTransfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Tables;

[Collection("Table management PostgreSQL HTTP")]
public sealed class TableManagementHttpTests : IAsyncLifetime
{
    private readonly TableManagementTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task SessionAndCapabilityBoundariesReturn401And403()
    {
        var terminalId = Guid.NewGuid();
        var expiredCookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: true);
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, canMutate: false, expired: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var missing = await client.GetAsync(Prefix(terminalId) + "/zones");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var expiredRequest = Request(HttpMethod.Get, Prefix(terminalId) + "/zones", expiredCookie);
        using var expired = await client.SendAsync(expiredRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        using var readableRequest = Request(HttpMethod.Get, Prefix(terminalId) + "/zones", readOnlyCookie);
        using var readable = await client.SendAsync(readableRequest);
        Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

        using var deniedRequest = JsonRequest(
            HttpMethod.Post,
            Prefix(terminalId) + "/zones",
            readOnlyCookie,
            new CreateZoneRequest(0, "DENIED", "Denied"));
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var envelope = await denied.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>();
        Assert.Equal("FORBIDDEN", envelope!.Error.Code);
        Assert.Equal(1L, await _database.ScalarAsync<long>(
            "SELECT count(*) FROM identity.denial_events WHERE permission_code = 'floorplan.manage';"));
    }

    [Fact]
    public async Task ZoneAndTableCrudUseAuthoritativeVersionsAndLeaveNoPartialStaleUpdate()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var zone = await PostAsync<ZoneDto>(
            client,
            Prefix(terminalId) + "/zones",
            cookie,
            new CreateZoneRequest(0, "SALON", "Salon", 1));
        Assert.True(zone.RowVersion > 0);

        using (var updateZoneRequest = JsonRequest(
            HttpMethod.Put,
            $"{Prefix(terminalId)}/zones/{zone.ZoneId:D}",
            cookie,
            new UpdateZoneRequest(zone.RowVersion, "SALON", "Ana Salon", 2, true)))
        using (var updateZone = await client.SendAsync(updateZoneRequest))
        {
            Assert.Equal(HttpStatusCode.OK, updateZone.StatusCode);
            zone = (await updateZone.Content.ReadFromJsonAsync<ZoneDto>())!;
        }

        var table = await PostAsync<TableDto>(
            client,
            Prefix(terminalId) + "/tables",
            cookie,
            new CreateTableRequest(0, "S-09", zone.ZoneId, 4));
        Assert.Equal(1, table.RowVersion);
        Assert.Contains("SetOccupied", table.AllowedCommands);

        using (var listRequest = Request(HttpMethod.Get, Prefix(terminalId) + "/tables", cookie))
        using (var list = await client.SendAsync(listRequest))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Contains(
                (await list.Content.ReadFromJsonAsync<TableDto[]>())!,
                candidate => candidate.TableId == table.TableId);
        }

        using (var updateTableRequest = JsonRequest(
            HttpMethod.Put,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}",
            cookie,
            new UpdateTableRequest(table.RowVersion, table.TableNumber, zone.ZoneId, 6, true)))
        using (var updateTable = await client.SendAsync(updateTableRequest))
        {
            Assert.Equal(HttpStatusCode.OK, updateTable.StatusCode);
            table = (await updateTable.Content.ReadFromJsonAsync<TableDto>())!;
        }
        Assert.Equal(2, table.RowVersion);
        Assert.Equal(6, table.Capacity);

        using (var staleRequest = JsonRequest(
            HttpMethod.Put,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}",
            cookie,
            new UpdateTableRequest(1, table.TableNumber, zone.ZoneId, 99, true)))
        using (var stale = await client.SendAsync(staleRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var error = await stale.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>();
            Assert.Equal("CONCURRENT_MODIFICATION", error!.Error.Code);
        }

        using (var readRequest = Request(
            HttpMethod.Get,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}",
            cookie))
        using (var read = await client.SendAsync(readRequest))
        {
            var authoritative = await read.Content.ReadFromJsonAsync<TableDto>();
            Assert.Equal(6, authoritative!.Capacity);
            Assert.Equal(2, authoritative.RowVersion);
        }

        using (var statusRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}/status",
            cookie,
            new ChangeTableStatusRequest(table.RowVersion, "Occupied")))
        using (var status = await client.SendAsync(statusRequest))
        {
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            table = (await status.Content.ReadFromJsonAsync<TableDto>())!;
        }
        Assert.Equal("Occupied", table.Status);
        Assert.Equal(3, table.RowVersion);

        using (var pointerRequest = Request(
            HttpMethod.Get,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}/current-pointer",
            cookie))
        using (var pointer = await client.SendAsync(pointerRequest))
        {
            Assert.Equal(HttpStatusCode.OK, pointer.StatusCode);
            Assert.Equal(table.TableId, (await pointer.Content.ReadFromJsonAsync<TablePointerDto>())!.TableId);
        }

        var disposableZone = await PostAsync<ZoneDto>(
            client,
            Prefix(terminalId) + "/zones",
            cookie,
            new CreateZoneRequest(0, "TEMP", "Temporary"));
        using var deleteRequest = Request(
            HttpMethod.Delete,
            $"{Prefix(terminalId)}/zones/{disposableZone.ZoneId:D}?expectedRowVersion={disposableZone.RowVersion}",
            cookie);
        using var deleted = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task ReservationTransferMergeAndUnmergeExecuteThroughRealPostgresqlServices()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var reservationTable = await CreateTableAsync(client, terminalId, cookie, "R-01");
        var reservation = await PostAsync<TableReservationResult>(
            client,
            Prefix(terminalId) + "/reservations",
            cookie,
            new CreateTableReservationRequest(
                reservationTable.TableId,
                reservationTable.RowVersion,
                null,
                "Dinner reservation",
                2));

        // Found by an independent audit (2026-09-07): TableReservationResult
        // never carried the reservation's own row version, so no real client
        // could ever populate Claim/Cancel/Expire's ExpectedReservationRowVersion
        // — this test itself used to hardcode the literal 1 below, the exact
        // same unfounded assumption a real client could not make. There was
        // also no GET to recover it later; both are fixed and exercised here.
        Assert.True(reservation.ReservationRowVersion > 0);
        using (var getRequest = Request(
            HttpMethod.Get, $"{Prefix(terminalId)}/reservations/{reservation.ReservationId:D}", cookie))
        using (var getResponse = await client.SendAsync(getRequest))
        {
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<TableReservationDto>();
            Assert.Equal(reservation.ReservationRowVersion, fetched!.RowVersion);
            Assert.Equal(TableReservationStatus.Active, fetched.Status);
        }

        // Found by an independent audit (2026-09-07): the plain table
        // list/detail DTO (unlike FloorPlanTableDto) never carried the
        // active reservation's id/row version either, so PosTerminal's
        // Claim/Cancel buttons stayed permanently disabled even after the
        // backend became reachable — no client request could ever be built.
        using (var tableGetRequest = Request(
            HttpMethod.Get, $"{Prefix(terminalId)}/tables/{reservationTable.TableId:D}", cookie))
        using (var tableGetResponse = await client.SendAsync(tableGetRequest))
        {
            var fetchedTable = await tableGetResponse.Content.ReadFromJsonAsync<TableDto>();
            Assert.Equal(reservation.ReservationId, fetchedTable!.ActiveReservationId);
            Assert.Equal(reservation.ReservationRowVersion, fetchedTable.ReservationRowVersion);
        }
        using (var tableListRequest = Request(HttpMethod.Get, $"{Prefix(terminalId)}/tables", cookie))
        using (var tableListResponse = await client.SendAsync(tableListRequest))
        {
            var listed = (await tableListResponse.Content.ReadFromJsonAsync<TableDto[]>())!
                .Single(t => t.TableId == reservationTable.TableId);
            Assert.Equal(reservation.ReservationId, listed.ActiveReservationId);
            Assert.Equal(reservation.ReservationRowVersion, listed.ReservationRowVersion);
        }

        using (var staleCancelRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/reservations/{reservation.ReservationId:D}/cancel",
            cookie,
            new CancelTableReservationRequest(
                reservation.ReservationRowVersion + 1, reservationTable.RowVersion, "Stale cancellation")))
        using (var staleCancel = await client.SendAsync(staleCancelRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleCancel.StatusCode);
        }
        Assert.Equal("Active", await _database.ScalarAsync<string>(
            $"SELECT status FROM table_mgmt.table_reservations WHERE table_reservation_id = '{reservation.ReservationId:D}';"));

        var cancelled = await PostAsync<TableReservationReleaseResult>(
            client,
            $"{Prefix(terminalId)}/reservations/{reservation.ReservationId:D}/cancel",
            cookie,
            new CancelTableReservationRequest(reservation.ReservationRowVersion, reservation.NewTableRowVersion, "Guest cancelled"));
        Assert.Equal(TableReservationStatus.Cancelled, cancelled.NewStatus);

        var claimTable = await CreateTableAsync(client, terminalId, cookie, "R-02");
        var claimReservation = await PostAsync<TableReservationResult>(
            client,
            Prefix(terminalId) + "/reservations",
            cookie,
            new CreateTableReservationRequest(claimTable.TableId, claimTable.RowVersion, null, "Walk-in"));
        var claimed = await PostAsync<TableReservationReleaseResult>(
            client,
            $"{Prefix(terminalId)}/reservations/{claimReservation.ReservationId:D}/claim",
            cookie,
            new ClaimTableReservationRequest(claimReservation.ReservationRowVersion, claimReservation.NewTableRowVersion, null));
        Assert.Equal(TableReservationStatus.Claimed, claimed.NewStatus);

        var expireTable = await CreateTableAsync(client, terminalId, cookie, "R-03");
        var expireReservation = await PostAsync<TableReservationResult>(
            client,
            Prefix(terminalId) + "/reservations",
            cookie,
            new CreateTableReservationRequest(expireTable.TableId, expireTable.RowVersion, null, "Timed hold"));
        var expired = await PostAsync<TableReservationReleaseResult>(
            client,
            $"{Prefix(terminalId)}/reservations/{expireReservation.ReservationId:D}/expire",
            cookie,
            new ExpireTableReservationRequest(expireReservation.ReservationRowVersion, expireReservation.NewTableRowVersion, "Hold expired"));
        Assert.Equal(TableReservationStatus.Expired, expired.NewStatus);

        var source = await CreateTableAsync(client, terminalId, cookie, "T-01");
        var target = await CreateTableAsync(client, terminalId, cookie, "T-02");
        source = await ChangeStatusAsync(client, terminalId, cookie, source, "Occupied");
        var transfer = await PostAsync<TableTransferResult>(
            client,
            Prefix(terminalId) + "/transfers",
            cookie,
            new TransferTableRequest(
                source.TableId,
                source.RowVersion,
                target.TableId,
                target.RowVersion,
                "Move service"));
        Assert.Equal(source.TableId, transfer.SourceTableId);
        Assert.Equal(target.TableId, transfer.TargetTableId);

        var primary = await CreateTableAsync(client, terminalId, cookie, "M-01");
        var participant = await CreateTableAsync(client, terminalId, cookie, "M-02");
        participant = await ChangeStatusAsync(client, terminalId, cookie, participant, "Occupied");
        await _database.SeedOpenOrderAndBillAsync(participant.TableId);
        var merge = await PostAsync<TableMergeResult>(
            client,
            Prefix(terminalId) + "/merges",
            cookie,
            new MergeTablesRequest(
                primary.TableId,
                primary.RowVersion,
                [new TableMergeParticipantRequest(participant.TableId, participant.RowVersion)],
                "Join tables"));
        Assert.Equal(primary.TableId, merge.PrimaryTableId);
        Assert.Contains(participant.TableId, merge.MergedTableIds);

        var unmerge = await PostAsync<TableUnmergeResult>(
            client,
            $"{Prefix(terminalId)}/merges/{merge.MergeGroupId:D}/unmerge",
            cookie,
            new UnmergeTablesRequest(
                merge.NewPrimaryRowVersion,
                [new TableMergeParticipantRequest(
                    participant.TableId,
                    merge.NewParticipantRowVersions[participant.TableId])],
                "Separate tables"));
        Assert.Equal(merge.MergeGroupId, unmerge.MergeGroupId);
    }

    [Fact]
    public async Task ChangingStatusOffAReservedTableReleasesTheReservationAndAllowsReReservation()
    {
        // Found by an independent audit (2026-09-07): the generic status
        // endpoint was the ONLY UI-reachable way to release a Reserved table
        // (Claim/Cancel/Expire needed a reservation row version no client
        // could ever supply before this same wave) but it never touched
        // table_mgmt.table_reservations, so the Active row stayed behind and
        // CreateReservationAsync's own guard permanently blocked the table
        // from ever being reserved again.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var table = await CreateTableAsync(client, terminalId, cookie, "R-04");
        var reservation = await PostAsync<TableReservationResult>(
            client,
            Prefix(terminalId) + "/reservations",
            cookie,
            new CreateTableReservationRequest(table.TableId, table.RowVersion, null, "No-show risk", 2));

        var released = await ChangeStatusAsync(
            client, terminalId, cookie,
            table with { RowVersion = reservation.NewTableRowVersion },
            "Available");
        Assert.Equal("Available", released.Status);
        Assert.Null(released.ActiveReservationId);
        Assert.Null(released.ReservationRowVersion);

        Assert.Equal("Cancelled", await _database.ScalarAsync<string>(
            $"SELECT status FROM table_mgmt.table_reservations WHERE table_reservation_id = '{reservation.ReservationId:D}';"));

        // The table can be reserved again — before this fix, CreateReservationAsync
        // would reject this with "table already has an active reservation".
        var secondReservation = await PostAsync<TableReservationResult>(
            client,
            Prefix(terminalId) + "/reservations",
            cookie,
            new CreateTableReservationRequest(table.TableId, released.RowVersion, null, "Retry booking", 2));
        Assert.NotEqual(reservation.ReservationId, secondReservation.ReservationId);
    }

    [Fact]
    public async Task ChangingStatusToReservedThroughTheGenericEndpointIsRejected()
    {
        // Found by an independent audit (2026-09-09): this generic endpoint
        // only requires tables.status, not tables.reserve, so setting
        // status=Reserved here bypassed the dedicated reservation permission
        // and never created a table_mgmt.table_reservations row — leaving a
        // sourceless Reserved table no dedicated endpoint could ever release.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var table = await CreateTableAsync(client, terminalId, cookie, "R-05");

        using var request = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}/status",
            cookie,
            new ChangeTableStatusRequest(table.RowVersion, "Reserved"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>();
        Assert.Equal("DOMAIN_CONFLICT", envelope!.Error.Code);

        Assert.Equal("Available", await _database.ScalarAsync<string>(
            $"SELECT current_status FROM table_mgmt.tables WHERE table_id = '{table.TableId:D}';"));
        Assert.Equal(0L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM table_mgmt.table_reservations WHERE table_id = '{table.TableId:D}';"));
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddTableManagementExperience();
        var app = builder.Build();
        app.MapTableManagementApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<TableDto> CreateTableAsync(
        HttpClient client,
        Guid terminalId,
        string cookie,
        string tableNumber)
        => await PostAsync<TableDto>(
            client,
            Prefix(terminalId) + "/tables",
            cookie,
            new CreateTableRequest(0, tableNumber, null, 4));

    private static async Task<TableDto> ChangeStatusAsync(
        HttpClient client,
        Guid terminalId,
        string cookie,
        TableDto table,
        string status)
    {
        using var request = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tables/{table.TableId:D}/status",
            cookie,
            new ChangeTableStatusRequest(table.RowVersion, status));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TableDto>())!;
    }

    private static async Task<TResponse> PostAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie,
        object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"POST {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(
        HttpMethod method,
        string path,
        string cookie,
        object body)
    {
        var request = Request(method, path, cookie);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static string Prefix(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/table-management";
}

[CollectionDefinition("Table management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class TableManagementPostgresqlDefinition;

internal sealed class TableManagementTestDatabase
{
    private readonly string _databaseName = "alkaros_rmd013_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? "localhost";
        var port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var parsedPort)
            ? parsedPort
            : 5432;
        var user = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD");
        var maintenanceConnection = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Username = user,
            Password = password,
            Database = "postgres",
        }.ConnectionString;

        await using (var maintenance = NpgsqlDataSource.Create(maintenanceConnection))
        {
            await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
            await ExecuteAsync(maintenance, $"CREATE DATABASE {_databaseName};");
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(maintenanceConnection)
        {
            Database = _databaseName,
        }.ConnectionString;
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
            return;

        var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres",
        }.ConnectionString;
        await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
    }

    public async Task<string> SeedSessionAsync(Guid terminalId, bool canMutate, bool expired)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Table API Test', true);

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, @created_at, @expires_at);
            """,
            ("user_id", userId),
            ("username", "table-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash),
            ("created_at", expired ? DateTimeOffset.UtcNow.AddHours(-2) : DateTimeOffset.UtcNow),
            ("expires_at", expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1)));

        if (canMutate)
        {
            var roleId = Guid.NewGuid();
            await ExecuteAsync(
                DataSource,
                """
                INSERT INTO identity.permissions (permission_id, code, name)
                SELECT gen_random_uuid(), code, code FROM (VALUES
                    ('floorplan.manage'), ('tables.status'), ('tables.reserve'),
                    ('tables.transfer'), ('tables.merge')) AS c(code)
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO identity.roles (role_id, code, name)
                VALUES (@role_id, @role_code, 'Table API Test Role');

                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT gen_random_uuid(), @role_id, permission_id
                FROM identity.permissions
                WHERE code IN ('floorplan.manage', 'tables.status', 'tables.reserve',
                               'tables.transfer', 'tables.merge');

                INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
                VALUES (@user_role_id, @user_id, @role_id);
                """,
                ("role_id", roleId),
                ("role_code", "table-api-role-" + suffix),
                ("user_role_id", Guid.NewGuid()),
                ("user_id", userId));
        }

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    public async Task SeedOpenOrderAndBillAsync(Guid tableId)
    {
        var orderId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO orders.orders (
                order_id, source, table_id, status, confirmation_status, order_number,
                created_at, updated_at)
            VALUES (
                @order_id, 'Cashier', @table_id, 'Draft', 'NotRequired', @order_number,
                now(), now());

            INSERT INTO billing.bills (
                bill_id, bill_number, table_id, order_id, status, opened_at, created_at, updated_at)
            VALUES (
                @bill_id, @bill_number, @table_id, @order_id, 'Open', now(), now(), now());
            """,
            ("order_id", orderId),
            ("table_id", tableId),
            ("order_number", "ORD-" + orderId.ToString("N")),
            ("bill_id", Guid.NewGuid()),
            ("bill_number", "BIL-" + orderId.ToString("N")));
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Scalar query returned null."));
    }

    private NpgsqlDataSource DataSource
        => _dataSource ?? throw new InvalidOperationException("Test database is not initialized.");

    private async Task ApplyMigrationsAsync()
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
            Assert.Single(files);
            await ExecuteAsync(DataSource, await File.ReadAllTextAsync(files[0]));
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlDataSource dataSource,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
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
