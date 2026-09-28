using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Audit.AuthorizationProbes;

/// <summary>
/// One real PostgreSQL database (every migration in database/MigrationComposition/order.json applied) and one
/// real DualScreenApplication host per probe class. Identity rows are seeded straight into the identity schema
/// with the same shapes the host tests use; every login-based probe goes through the real /api/v1/auth/login.
/// </summary>
public sealed class ProbeHarness : IAsyncLifetime
{
    public const string ProbePassword = "Probe-Password-399";

    // One production-strength hash, computed once and shared by every seeded login user.
    private static readonly string ProbePasswordHash = new PasswordHasher().Hash(ProbePassword);
    private readonly string _databaseName = "alkaros_rmd399_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd399-webroot-{Guid.NewGuid():N}");
    private NpgsqlDataSource? _dataSource;
    private WebApplication? _app;
    private int _clientAddress;

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
        Client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(address) };
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

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? cookie, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        // A distinct source address per request keeps the per-IP limiters (login, qr-session, ...) out of the
        // measurement; the terminal-partitioned limiters are avoided by using fresh terminal ids.
        var octet = Interlocked.Increment(ref _clientAddress);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", $"198.51.{(octet / 250) % 250}.{(octet % 250) + 1}");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, string? cookie, object body)
        => SendAsync(HttpMethod.Post, path, cookie, body);

    public Task<HttpResponseMessage> GetAsync(string path, string? cookie)
        => SendAsync(HttpMethod.Get, path, cookie);

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Joins every Set-Cookie of a response into one Cookie request header value.</summary>
    public static string CookiesOf(HttpResponseMessage response)
        => string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(value => value.Split(';')[0]).Where(pair => !pair.EndsWith('='))
            : []);

    // ---- Seeding ----------------------------------------------------------------------------------------------

    /// <summary>A real user (PBKDF2 password <see cref="ProbePassword"/>) holding the seeded role <paramref name="roleCode"/>.</summary>
    public async Task<(Guid UserId, string Username)> SeedUserInRoleAsync(string roleCode)
    {
        var userId = Guid.NewGuid();
        var username = "rmd399-" + userId.ToString("N")[..10];
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, @password_hash, 'RMD399 Probe User', true);
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), @user_id, role_id FROM identity.roles WHERE code = @role_code;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("password_hash", ProbePasswordHash);
        command.Parameters.AddWithValue("role_code", roleCode);
        await command.ExecuteNonQueryAsync();
        Assert.True(await ScalarAsync<long>(
            "SELECT count(*) FROM identity.user_roles WHERE user_id = @id;", ("id", userId)) == 1,
            $"Seeded role '{roleCode}' does not exist.");
        return (userId, username);
    }

    /// <summary>A cashier-device session on <paramref name="terminalId"/> for a new user whose own role holds exactly <paramref name="permissionCodes"/>.</summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierAsync(Guid terminalId, params string[] permissionCodes)
    {
        var userId = await SeedUserWithCustomRoleAsync(permissionCodes);
        var cookie = await SeedSessionAsync(userId, $"cashier:{terminalId:D}", DualScreenApplication.CashierCookieName);
        return (userId, cookie);
    }

    public async Task<Guid> SeedUserWithCustomRoleAsync(params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var suffix = userId.ToString("N")[..10];
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'RMD399 Probe User', true);
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'RMD399 Probe Role');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (gen_random_uuid(), @user_id, @role_id);
            """))
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("username", "rmd399-" + suffix);
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("role_code", "rmd399-role-" + suffix);
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

        return userId;
    }

    /// <summary>An identity.device_sessions row for <paramref name="deviceId"/>; returns the matching Cookie header value.</summary>
    public async Task<string> SeedSessionAsync(Guid userId, string deviceId, string cookieName)
    {
        var rawToken = "rmd399-" + Guid.NewGuid().ToString("N");
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("device_id", deviceId);
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        await command.ExecuteNonQueryAsync();
        return $"{cookieName}={rawToken}";
    }

    /// <summary>A customer-display session paired with <paramref name="terminalId"/>; returns the display Cookie header value.</summary>
    public async Task<string> SeedDisplaySessionAsync(Guid terminalId)
    {
        var rawToken = "rmd399-display-" + Guid.NewGuid().ToString("N");
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO customer_display.terminals (terminal_id, created_at, updated_at) VALUES (@terminal_id, now(), now()) ON CONFLICT DO NOTHING;
            INSERT INTO customer_display.display_sessions (session_id, display_id, terminal_id, token_hash, created_at, expires_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), @terminal_id, @token_hash, now(), now() + interval '1 hour');
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        command.Parameters.AddWithValue("token_hash", DualScreenToken.Hash(rawToken));
        await command.ExecuteNonQueryAsync();
        return $"{DualScreenApplication.DisplayCookieName}={rawToken}";
    }

    /// <summary>A real login through /api/v1/auth/login; returns the response and every cookie it set.</summary>
    public async Task<(HttpResponseMessage Response, string Cookies)> LoginAsync(Guid terminalId, string username, string password)
    {
        var response = await PostAsync("/api/v1/auth/login", null, new { TerminalId = terminalId, Username = username, Password = password });
        return (response, CookiesOf(response));
    }

    /// <summary>A Submitted order with one 100.00 item; <paramref name="servingUserId"/> is the check's server (null = unassigned).</summary>
    public async Task<(Guid OrderId, Guid ItemId, long RowVersion)> SeedOrderAsync(Guid? servingUserId)
    {
        var productId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Probe Item', 1, 1, 100.00);
            """))
        {
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..10]);
            await command.ExecuteNonQueryAsync();
        }

        var tableId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Occupied');
            """))
        {
            command.Parameters.AddWithValue("table_id", tableId);
            command.Parameters.AddWithValue("table_number", "T-" + Guid.NewGuid().ToString("N")[..8]);
            await command.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(), orderId: orderId, productId: productId, productNameSnapshot: "Probe Item",
            quantity: 1, unitPrice: 100.00m, taxRate: 0m);
        var order = new Order(
            orderId, OrderSource.Cashier, "ORD-" + Guid.NewGuid().ToString("N")[..10], [item],
            tableId: tableId, servingUserId: servingUserId);
        await new PostgresOrderRepository(DataSource).AddAsync(order);
        var rowVersion = await ScalarAsync<long>("SELECT row_version FROM orders.orders WHERE order_id = @id;", ("id", orderId));
        return (orderId, item.Id, rowVersion);
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    // ---- Read-side ground truth (straight from the tables, never from the code under audit) ----------------------

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

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
