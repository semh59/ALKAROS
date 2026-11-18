using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.KitchenOperations;
using ALKAROS.Host.Tests.Fixtures;
using ALKAROS.Identity.Authentication;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

/// <summary>A note typed at the till travels with the line to the kitchen ticket, the kitchen board API and the printer job.</summary>
[Collection("Host database password environment")]
public sealed class ItemNoteKitchenPathHttpTests : IAsyncLifetime
{
    private const string Password = "Test-password-42";
    private const string KitchenStationId = "kitchen-note-test";
    private const string Forwarded = "198.51.100.77";
    private readonly TestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-item-note-{Guid.NewGuid():N}");
    private readonly string? _originalKitchenStation = Environment.GetEnvironmentVariable(
        DualScreenApplication.KitchenStationEnvironmentVariable);
    private readonly Guid _productId = Guid.NewGuid();
    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        await File.WriteAllTextAsync(Path.Combine(_webRoot, "index.html"), "<!doctype html>");
        Environment.SetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable, KitchenStationId);
        await _database.InitializeMigratedAsync(FindRepositoryRoot());
        var uri = new Uri(_database.Url);
        _dataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        Environment.SetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable, _originalKitchenStation);
        await _database.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task ANoteTypedAtTheTillReachesTheTicketTheKitchenBoardAndThePrinterJob()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cookie = await LoginAsync(client, terminalId);
        var (orderId, revision) = await StartOrderAsync(client, terminalId, cookie);

        using var added = await Send(client, HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items", cookie,
            new { productId = _productId, quantity = 1, expectedRevision = revision, specialInstructions = "soğansız" });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        revision = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64();
        using var submitted = await Send(client, HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/submit", cookie,
            new { operationId = "note-chain-1", expectedRevision = revision });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        Assert.Equal("soğansız", await ScalarAsync<string>(
            "SELECT i.notes FROM kitchen.kitchen_ticket_items i JOIN kitchen.kitchen_tickets t ON t.id = i.ticket_id WHERE t.order_id = @order_id;",
            ("order_id", orderId)));

        using var board = await Send(client, HttpMethod.Get,
            $"/api/v1/terminals/{terminalId:D}/kitchen-operations/tickets?stationId={KitchenStationId}", cookie, null);
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        var ticket = (await board.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single();
        Assert.Equal("soğansız", ticket.GetProperty("items").EnumerateArray().Single().GetProperty("notes").GetString());

        var printQueue = new PostgresPrintQueueRepository(_dataSource);
        await BridgeAsync(printQueue);
        var job = Assert.Single(await printQueue.GetByTicketIdAsync(ticket.GetProperty("id").GetGuid()));
        Assert.Contains("NOT: soğansız", job.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAddWithoutANoteReachesTheTicketWithoutOne()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cookie = await LoginAsync(client, terminalId);
        var (orderId, revision) = await StartOrderAsync(client, terminalId, cookie);

        using var added = await Send(client, HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items", cookie,
            new { productId = _productId, quantity = 1, expectedRevision = revision });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        revision = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64();
        using var submitted = await Send(client, HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/submit", cookie,
            new { operationId = "note-chain-2", expectedRevision = revision });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM kitchen.kitchen_ticket_items i JOIN kitchen.kitchen_tickets t ON t.id = i.ticket_id WHERE t.order_id = @order_id AND i.notes IS NULL;",
            ("order_id", orderId)));
    }

    [Fact]
    public async Task ANoteOverTheLimitIsRefusedInTurkishAndNothingIsAdded()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cookie = await LoginAsync(client, terminalId);
        var (orderId, revision) = await StartOrderAsync(client, terminalId, cookie);

        using var refused = await Send(client, HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items", cookie,
            new { productId = _productId, quantity = 1, expectedRevision = revision, specialInstructions = new string('a', DualScreenStore.MaxSpecialInstructionsLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var error = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("VALIDATION_FAILED", error.GetProperty("code").GetString());
        Assert.Equal("Not izin verilen aralığın dışında.", error.GetProperty("message").GetString());
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM orders.order_items WHERE order_id = @order_id;", ("order_id", orderId)));
    }

    [Fact]
    public async Task ANoteWithALineBreakIsRefusedInTurkishAndTheTillSnapshotShowsAnAcceptedNote()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cookie = await LoginAsync(client, terminalId);
        var (orderId, revision) = await StartOrderAsync(client, terminalId, cookie);
        var itemsPath = $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items";

        using var refused = await Send(client, HttpMethod.Post, itemsPath, cookie,
            new { productId = _productId, quantity = 1, expectedRevision = revision, specialInstructions = "soğansız\naz pişmiş" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("Not geçersiz veya eksik.", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("message").GetString());

        using var accepted = await Send(client, HttpMethod.Post, itemsPath, cookie,
            new { productId = _productId, quantity = 1, expectedRevision = revision, specialInstructions = "soğansız" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var snapshot = await Send(client, HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/orders/active", cookie, null);
        var line = (await snapshot.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines").EnumerateArray().Single();
        Assert.Equal("soğansız", line.GetProperty("specialInstructions").GetString());
    }

    private async Task BridgeAsync(PostgresPrintQueueRepository printQueue)
    {
        var bridged = await KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync(
            _dataSource, new PostgresKitchenTicketRepository(_dataSource), new PostgresPrinterRepository(_dataSource),
            new PrintQueueService(printQueue), CancellationToken.None);
        Assert.Equal(1, bridged);
    }

    private static async Task<(Guid OrderId, long Revision)> StartOrderAsync(HttpClient client, Guid terminalId, string cookie)
    {
        using var started = await Send(client, HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders", cookie, new { });
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var body = await started.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("orderId").GetGuid(), body.GetProperty("revision").GetInt64());
    }

    private static async Task<string> LoginAsync(HttpClient client, Guid terminalId)
    {
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent(new { terminalId, username = "cashier", password = Password }),
        };
        AddTrustedForwarding(login);
        using var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(DualScreenApplication.CashierCookieName + "=", StringComparison.Ordinal))
            .Split(';', 2)[0];
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string cookie, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        AddTrustedForwarding(request);
        if (body is not null)
            request.Content = JsonContent(body);
        return client.SendAsync(request);
    }

    private async Task SeedAsync()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, 'cashier', @password_hash, 'Cashier', true);
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, 'item-note-role', 'Kasiyer');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (gen_random_uuid(), @user_id, @role_id);
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), @role_id, permission_id FROM identity.permissions WHERE code IN ('orders.create', 'orders.send');
            INSERT INTO catalog.products (product_id, sku, name, category_id, tax_profile_id, product_type, stock_mode, active, display_order, current_price)
            VALUES (@product_id, 'NOTE-001', 'Adana', NULL, NULL, 1, 1, true, 0, 100.00);
            INSERT INTO kitchen.printers (id, name, station_id, ip_address, port, is_active, created_at)
            VALUES (gen_random_uuid(), 'Note test printer', @station, '10.0.0.9', 9100, TRUE, now());
            """,
            ("user_id", userId),
            ("role_id", roleId),
            ("password_hash", new PasswordHasher(10_000).Hash(Password)),
            ("product_id", _productId),
            ("station", KitchenStationId));
    }

    private async Task<WebApplication> StartAsync()
    {
        var uri = new Uri(_database.Url);
        var app = DualScreenApplication.Build(new DualScreenOptions(
            new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port,
                Database = uri.AbsolutePath.Trim('/'),
                Username = uri.UserInfo,
                Password = _database.PsqlOptions.Password,
            }.ConnectionString,
            _webRoot,
            "http://127.0.0.1:0",
            [System.Net.IPAddress.Loopback]));
        await app.StartAsync();
        return app;
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private static StringContent JsonContent(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static void AddTrustedForwarding(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", Forwarded);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
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
