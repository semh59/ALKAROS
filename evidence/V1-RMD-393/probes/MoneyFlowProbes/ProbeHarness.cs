using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Audit.MoneyFlowProbes;

/// <summary>
/// One real PostgreSQL database (every migration in database/MigrationComposition/order.json applied) and one
/// real DualScreenApplication host per probe class. Seeding mirrors tests/Host/Experience/CashSession and
/// tests/Host/Experience/PaymentTender so the probes exercise exactly the production composition.
/// </summary>
public sealed class ProbeHarness : IAsyncLifetime
{
    private readonly string _databaseName = "alkaros_rmd393_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd393-webroot-{Guid.NewGuid():N}");
    private NpgsqlDataSource? _dataSource;
    private WebApplication? _app;

    public string ConnectionString { get; private set; } = string.Empty;

    public NpgsqlDataSource DataSource => _dataSource ?? throw new InvalidOperationException("Not initialized.");

    public WebApplication App => _app ?? throw new InvalidOperationException("Not initialized.");

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Probe</body></html>");

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

        _app = DualScreenApplication.Build(new DualScreenOptions(
            ConnectionString, _webRoot, "http://127.0.0.1:0", TrustedProxies: [IPAddress.Loopback]));
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
            await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
            await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
        }
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    // ---- HTTP -------------------------------------------------------------------------------------------------

    public async Task<HttpResponseMessage> PostAsync(string path, string? cookie, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        Decorate(request, cookie);
        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PutAsync(string path, string? cookie, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        Decorate(request, cookie);
        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetAsync(string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        Decorate(request, cookie);
        return await Client.SendAsync(request);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static void Decorate(HttpRequestMessage request, string? cookie)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
    }

    // ---- Seeding ----------------------------------------------------------------------------------------------

    /// <summary>A cashier device session on <paramref name="terminalId"/> holding exactly <paramref name="permissionCodes"/>.</summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierAsync(Guid terminalId, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var rawToken = "rmd393-" + Guid.NewGuid().ToString("N");
        var suffix = userId.ToString("N")[..10];
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'RMD393 Probe User', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'RMD393 Probe Role');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (gen_random_uuid(), @user_id, @role_id);
            """))
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("username", "rmd393-" + suffix);
            command.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
            command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("role_code", "rmd393-role-" + suffix);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var code in permissionCodes)
        {
            await using var grant = DataSource.CreateCommand(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT gen_random_uuid(), @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """);
            grant.Parameters.AddWithValue("role_id", roleId);
            grant.Parameters.AddWithValue("code", code);
            var granted = await grant.ExecuteNonQueryAsync();
            Assert.True(granted == 1, $"Permission code '{code}' does not exist in identity.permissions.");
        }

        return (userId, $"{DualScreenApplication.CashierCookieName}={rawToken}");
    }

    /// <summary>A real payable Bill (catalog product + table + order + bill, status Open), same shape as the host tests.</summary>
    public async Task<Guid> SeedBillAsync(decimal payable)
    {
        var productId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Probe Item', 1, 1, @price);
            """))
        {
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..10]);
            command.Parameters.AddWithValue("price", payable);
            await command.ExecuteNonQueryAsync();
        }

        var tableId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Available');
            """))
        {
            command.Parameters.AddWithValue("table_id", tableId);
            command.Parameters.AddWithValue("table_number", "T-" + Guid.NewGuid().ToString("N")[..8]);
            await command.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(), orderId: orderId, productId: productId, productNameSnapshot: "Probe Item",
            quantity: 1, unitPrice: payable, taxRate: 0m);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + Guid.NewGuid().ToString("N")[..10], [item], tableId: tableId);
        await new PostgresOrderRepository(DataSource).AddAsync(order);

        var billId = Guid.NewGuid();
        var bill = new Bill(
            id: billId,
            billNumber: "BILL-" + Guid.NewGuid().ToString("N")[..10],
            items: [BillItem.FromOrderItem(billId, order.Items[0])],
            tableId: tableId,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: "TRY");
        await new PostgresBillRepository(DataSource).AddAsync(bill);
        return billId;
    }

    public async Task<Guid> OpenCashSessionAsync(Guid terminalId, string cookie, decimal openingBalance)
    {
        var opened = await PostAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = openingBalance });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        return (await JsonAsync(opened)).GetProperty("cashSessionId").GetGuid();
    }

    // ---- Read-side ground truth (straight from the tables, never from the code under audit) ----------------------

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public Task<decimal> AllocatedTotalAsync(Guid billId) => ScalarAsync<decimal>(
        "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_allocations WHERE bill_id = @id;", ("id", billId));

    public Task<string> BillStatusAsync(Guid billId) => ScalarAsync<string>(
        "SELECT status FROM billing.bills WHERE bill_id = @id;", ("id", billId));

    public Task<long> SaleLedgerCountAsync(Guid cashSessionId) => ScalarAsync<long>(
        "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @id AND type = 'Sale';", ("id", cashSessionId));

    public Task<long> PaymentCountAsync(Guid billId) => ScalarAsync<long>(
        "SELECT count(*) FROM payments.payments WHERE bill_id = @id;", ("id", billId));

    public Task<decimal> ExpectedCashFromLedgerAsync(Guid cashSessionId) => ScalarAsync<decimal>(
        """
        SELECT COALESCE(SUM(CASE WHEN direction = 'In' THEN amount ELSE -amount END), 0)
        FROM cash.cash_transactions WHERE cash_session_id = @id AND type NOT IN ('CountAdjustment', 'ClosingDifference');
        """, ("id", cashSessionId));

    // ---- Migrations -------------------------------------------------------------------------------------------

    private async Task ApplyMigrationsAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString() ?? throw new InvalidOperationException("Migration ID is missing.");
            var files = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories);
            await ExecuteAsync(DataSource, await File.ReadAllTextAsync(files[0]));
        }
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
