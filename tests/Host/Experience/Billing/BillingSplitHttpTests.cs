using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillingSplitHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task EndpointsEnforceSessionPermissionAndAtomicConcurrency()
    {
        var terminalId = Guid.NewGuid();
        var mutableCookie = await _database.SeedSessionAsync(terminalId, canMutate: true);
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, canMutate: false);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var path = Path(terminalId, seeded.BillId);

        using (var missing = await client.GetAsync(path))
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using (var readableRequest = Request(HttpMethod.Get, path, readOnlyCookie))
        using (var readable = await client.SendAsync(readableRequest))
        {
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
            Assert.Empty((await readable.Content.ReadFromJsonAsync<BillSplitDesignDto>())!.AllowedCommands);
        }

        // Owner options come from the server (table seats), not client-fabricated
        // GUIDs (deep-analysis finding F-2).
        using (var ownersRequest = Request(HttpMethod.Get, path + "/owners", readOnlyCookie))
        using (var owners = await client.SendAsync(ownersRequest))
        {
            Assert.Equal(HttpStatusCode.OK, owners.StatusCode);
            var options = await owners.Content.ReadFromJsonAsync<IReadOnlyList<BillSplitOwnerOptionDto>>();
            Assert.Contains(options!, option => option.Kind == "Seat" && option.Id == seeded.SeatId);
        }

        var equalRequest = new SaveEqualSplitRequest(
            seeded.BillRowVersion,
            [],
            [new SplitOwnerRequest("Seat", seeded.SeatId), new SplitOwnerRequest("Person", Guid.NewGuid())]);
        using (var deniedRequest = JsonRequest(HttpMethod.Put, path + "/equal", readOnlyCookie, equalRequest))
        using (var denied = await client.SendAsync(deniedRequest))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var equal = await PutAsync<BillSplitDesignDto>(client, path + "/equal", mutableCookie, equalRequest);
        Assert.Equal("EqualByPerson", equal.Mode);
        Assert.Equal(equal.PayableAmount, equal.Allocations.Sum(allocation => allocation.Amount));
        Assert.Equal(equal.TaxTotal, equal.Allocations.Sum(allocation => allocation.TaxAmount));
        Assert.Contains(equal.Allocations, allocation => allocation.OwnerId == seeded.SeatId);

        var wrongVersions = Versions(equal);
        wrongVersions[0] = wrongVersions[0] with { RowVersion = wrongVersions[0].RowVersion + 1 };
        using (var staleAllocationRequest = JsonRequest(
            HttpMethod.Put,
            path + "/equal",
            mutableCookie,
            equalRequest with
            {
                ExpectedBillRowVersion = equal.BillRowVersion,
                ExpectedAllocations = wrongVersions,
            }))
        using (var staleAllocation = await client.SendAsync(staleAllocationRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleAllocation.StatusCode);
            var error = await staleAllocation.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>();
            Assert.Equal("allocation", error!.Error.Conflict!.Resource);
        }

        using (var staleRequest = JsonRequest(HttpMethod.Put, path + "/equal", mutableCookie, equalRequest))
        using (var stale = await client.SendAsync(staleRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var error = await stale.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>();
            Assert.Equal("CONCURRENT_MODIFICATION", error!.Error.Code);
            Assert.NotNull(error.Error.Conflict);
        }
        Assert.Equal(
            equal.Allocations.Select(allocation => allocation.AllocationId),
            await _database.AllocationIdsAsync(seeded.BillId));
    }

    [Fact]
    public async Task AllModesPersistRejectOverflowSurviveRestartAndClear()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true);
        var seeded = await _database.SeedBillAsync();
        var path = Path(terminalId, seeded.BillId);
        BillSplitDesignDto current;

        await using (var app = await StartAsync())
        using (var client = CreateClient(app))
        {
            current = await PutAsync<BillSplitDesignDto>(
                client,
                path + "/items",
                cookie,
                new SaveItemSplitRequest(
                    seeded.BillRowVersion,
                    [],
                    [
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), seeded.FirstItemId, 1m),
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), seeded.FirstItemId, 1m),
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), seeded.SecondItemId, 1m),
                    ]));
            Assert.Equal("ByItem", current.Mode);

            var expected = Versions(current);
            using (var overflowRequest = JsonRequest(
                HttpMethod.Put,
                path + "/items",
                cookie,
                new SaveItemSplitRequest(
                    current.BillRowVersion,
                    expected,
                    [
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), seeded.FirstItemId, 2m),
                        new ItemSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), seeded.FirstItemId, 1m),
                    ])))
            using (var overflow = await client.SendAsync(overflowRequest))
                Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
            Assert.Equal(current.Allocations.Select(allocation => allocation.AllocationId), await _database.AllocationIdsAsync(seeded.BillId));

            current = await PutAsync<BillSplitDesignDto>(
                client,
                path + "/amounts",
                cookie,
                new SaveAmountSplitRequest(
                    current.BillRowVersion,
                    Versions(current),
                    [
                        new AmountSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), 100m),
                        new AmountSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), 175m),
                    ]));
            Assert.Equal("ByAmount", current.Mode);

            current = await PutAsync<BillSplitDesignDto>(
                client,
                path + "/custom",
                cookie,
                new SaveCustomSplitRequest(
                    current.BillRowVersion,
                    Versions(current),
                    [
                        new CustomSplitTargetRequest(new SplitOwnerRequest("Seat", seeded.SeatId), 120m, null, null),
                        new CustomSplitTargetRequest(new SplitOwnerRequest("Person", Guid.NewGuid()), 155m, null, null),
                    ]));
            Assert.Equal("Custom", current.Mode);
        }

        await using (var restarted = await StartAsync())
        using (var client = CreateClient(restarted))
        {
            using var getRequest = Request(HttpMethod.Get, path, cookie);
            using var get = await client.SendAsync(getRequest);
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            current = (await get.Content.ReadFromJsonAsync<BillSplitDesignDto>())!;
            Assert.Equal("Custom", current.Mode);
            Assert.Equal(2, current.Allocations.Count);

            var cleared = await PostAsync<BillSplitDesignDto>(
                client,
                path + "/clear",
                cookie,
                new ClearSplitDesignRequest(current.BillRowVersion, Versions(current)));
            Assert.Equal("None", cleared.Mode);
            Assert.Empty(cleared.Allocations);

            await _database.SetBillStatusAsync(seeded.BillId, "Paid");
            using var paidRequest = JsonRequest(
                HttpMethod.Put,
                path + "/equal",
                cookie,
                new SaveEqualSplitRequest(
                    cleared.BillRowVersion,
                    [],
                    [new SplitOwnerRequest("Seat", seeded.SeatId), new SplitOwnerRequest("Person", Guid.NewGuid())]));
            using var paid = await client.SendAsync(paidRequest);
            Assert.Equal(HttpStatusCode.Conflict, paid.StatusCode);
            Assert.Equal(
                "UNSUPPORTED_BILL_STATE",
                (await paid.Content.ReadFromJsonAsync<BillingSplitErrorEnvelope>())!.Error.Code);
        }
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
        await app.StartAsync();
        return app;
    }

    private static List<AllocationVersionRequest> Versions(BillSplitDesignDto design)
        => design.Allocations.Select(allocation => new AllocationVersionRequest(allocation.AllocationId, allocation.RowVersion)).ToList();

    private static async Task<T> PutAsync<T>(HttpClient client, string path, string cookie, object body)
    {
        using var request = JsonRequest(HttpMethod.Put, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, string cookie, object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string cookie, object body)
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

    private static string Path(Guid terminalId, Guid billId)
        => $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/split-design";
}

public sealed class BillingSplitRegistrationTests
{
    [Fact]
    public void RegistrationPublishesVersionedDesignOnlyRoutes()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddBillingSplitExperience();
        using var app = builder.Build();
        app.MapBillingSplitApi();

        Assert.IsType<PostgresBillRepository>(app.Services.GetRequiredService<IBillRepository>());
        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();
        var prefix = "/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/split-design";
        AssertRoute(routes, "GET", prefix);
        AssertRoute(routes, "PUT", prefix + "/equal");
        AssertRoute(routes, "PUT", prefix + "/items");
        AssertRoute(routes, "PUT", prefix + "/amounts");
        AssertRoute(routes, "PUT", prefix + "/custom");
        AssertRoute(routes, "POST", prefix + "/clear");
    }

    private static void AssertRoute(IEnumerable<dynamic> routes, string method, string pattern)
    {
        Assert.Contains(routes, route =>
            string.Equals(((string?)route.Route)?.TrimEnd('/'), pattern.TrimEnd('/'), StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains(method, StringComparer.Ordinal));
    }
}

[CollectionDefinition("Billing split PostgreSQL HTTP", DisableParallelization = true)]
public sealed class BillingSplitPostgresqlDefinition;

internal sealed record SeededBill(
    Guid BillId,
    long BillRowVersion,
    Guid SeatId,
    Guid FirstItemId,
    Guid SecondItemId);

internal sealed class BillingSplitTestDatabase
{
    private readonly string _databaseName = "alkaros_rmd027_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var maintenanceConnection = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? "localhost",
            Port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var port) ? port : 5432,
            Username = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? "postgres",
            Password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD"),
            Database = "postgres",
        }.ConnectionString;
        await using (var maintenance = NpgsqlDataSource.Create(maintenanceConnection))
        {
            await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
            await ExecuteAsync(maintenance, $"CREATE DATABASE {_databaseName};");
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(maintenanceConnection) { Database = _databaseName }.ConnectionString;
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
        var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
        await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
    }

    public async Task<string> SeedSessionAsync(Guid terminalId, bool canMutate)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Billing API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "billing-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        if (canMutate)
        {
            var roleId = Guid.NewGuid();
            await ExecuteAsync(
                DataSource,
                """
                INSERT INTO identity.permissions (permission_id, code, name)
                VALUES (@permission_id, 'bills.split', 'Operational bill splitting') ON CONFLICT (code) DO NOTHING;
                INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Billing API Test Role');
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @role_permission_id, @role_id, permission_id FROM identity.permissions WHERE code = 'bills.split';
                INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
                VALUES (@user_role_id, @user_id, @role_id);
                """,
                ("permission_id", Guid.NewGuid()),
                ("role_id", roleId),
                ("role_code", "billing-api-role-" + suffix),
                ("role_permission_id", Guid.NewGuid()),
                ("user_role_id", Guid.NewGuid()),
                ("user_id", userId));
        }

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    public async Task<SeededBill> SeedBillAsync()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 2, 'Occupied');
            INSERT INTO table_mgmt.table_seats (seat_id, table_id, seat_number, label, x, y)
            VALUES (@seat_id, @table_id, 1, 'Seat 1', 0, 0);
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@first_product_id, @first_sku, 'Main course', 1, 1, 100),
                   (@second_product_id, @second_sku, 'Drink', 1, 1, 50);
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")),
            ("table_id", tableId),
            ("table_number", "T-" + tableId.ToString("N")[..6]),
            ("seat_id", seatId),
            ("first_product_id", firstProductId),
            ("first_sku", "SKU-" + firstProductId.ToString("N")[..8]),
            ("second_product_id", secondProductId),
            ("second_sku", "SKU-" + secondProductId.ToString("N")[..8]));

        var orderId = Guid.NewGuid();
        var firstOrderItem = new OrderItem(Guid.NewGuid(), orderId, firstProductId, "Main course", 2m, 100m, 10m);
        var secondOrderItem = new OrderItem(Guid.NewGuid(), orderId, secondProductId, "Drink", 1m, 50m, 10m);
        var order = new Order(
            orderId,
            OrderSource.Cashier,
            "ORD-" + orderId.ToString("N"),
            [firstOrderItem, secondOrderItem],
            tableId);
        await new PostgresOrderRepository(DataSource).AddAsync(order);

        var bill = Bill.FromOrder(Guid.NewGuid(), "BIL-" + orderId.ToString("N"), order);
        await new PostgresBillRepository(DataSource).AddAsync(bill);
        return new SeededBill(bill.Id, bill.RowVersion, seatId, bill.Items[0].Id, bill.Items[1].Id);
    }

    public async Task<IReadOnlyList<Guid>> AllocationIdsAsync(Guid billId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT bill_allocation_id FROM billing.bill_allocations WHERE bill_id = @bill_id ORDER BY created_at, bill_allocation_id;");
        command.Parameters.AddWithValue("bill_id", billId);
        var result = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetGuid(0));
        return result;
    }

    public Task SetBillStatusAsync(Guid billId, string status)
        => ExecuteAsync(
            DataSource,
            "UPDATE billing.bills SET status = @status WHERE bill_id = @bill_id;",
            ("status", status),
            ("bill_id", billId));

    private NpgsqlDataSource DataSource
        => _dataSource ?? throw new InvalidOperationException("Test database is not initialized.");

    private async Task ApplyMigrationsAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            System.IO.Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = System.IO.Path.Combine(root, "database", "migrations", "V1");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString() ?? throw new InvalidOperationException("Migration ID is missing.");
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
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
