using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.CustomerAccounts;
using ALKAROS.Host.Experience.CustomerAging;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using ALKAROS.Identity.DeviceSessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.CustomerAccounts.Tests;

/// <summary>
/// F0097 over the real host and a real PostgreSQL with every migration: the aging report ages the ledger oldest
/// charge first, shows the snapshot balances, and the snapshot job is registered and writes closing balances.
/// </summary>
[Collection("CustomerAccounts HTTP")]
public sealed class CustomerAgingHttpTests : IAsyncLifetime
{
    private const string ReportPath = "/api/v1/management/reports/customer-aging";
    private const string ViewerToken = "rmd880-viewer";
    private const string DeniedToken = "rmd880-denied";
    private const string SupervisorToken = "rmd880-supervisor";

    private readonly CustomerAccountsHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd880-webroot-{Guid.NewGuid():N}");

    static CustomerAgingHttpTests()
    {
        Environment.SetEnvironmentVariable(
            "ALKAROS_SECRET_ENVELOPE_MASTER_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
        await SeedManagerAsync(ViewerToken, "reports.view");
        await SeedManagerAsync(DeniedToken);
        await SeedSessionAsync(SupervisorToken, "supervisor:test", "reports.view");
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task OnlyASessionHoldingReportsViewReadsTheReport()
    {
        await using var app = await StartAsync();
        using var anonymous = CreateClient(app, null);
        using var denied = CreateClient(app, DeniedToken);
        using var viewer = CreateClient(app, ViewerToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ReportPath)).StatusCode);
        var forbidden = await denied.GetAsync(ReportPath);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("Cari yaşlandırma raporu için rapor görüntüleme izni gerekiyor.",
            (await forbidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(ReportPath)).StatusCode);
        using var supervisor = CreateClient(app, SupervisorToken);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync(ReportPath)).StatusCode);
    }

    [Fact]
    public async Task ACustomerWithoutAReadableProfileIsStillListedUnderANeutralName()
    {
        await using var app = await StartAsync();
        await Ledger(Guid.NewGuid(), "Charge", 10m, 2);
        using var viewer = CreateClient(app, ViewerToken);

        var body = await viewer.GetFromJsonAsync<JsonElement>(ReportPath);

        Assert.Equal(ReceivablesAgingEndpoints.UnknownCustomerName, Assert.Single(body.GetProperty("rows").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task TheReportAgesEachCustomerAndShowsTheBalancesFromTheSnapshotsTheJobWrote()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd880-cashier");
        await using var app = await StartAsync();
        using var cashierClient = CreateClient(app, null);
        var owing = await CreateCustomerAsync(cashierClient, terminalId, cashier, "Borçlu Müşteri");
        var prepaid = await CreateCustomerAsync(cashierClient, terminalId, cashier, "Peşin Ödeyen");
        await _database.SetCreditTermsAsync(owing.CustomerId, 1000m, 60);
        await Ledger(owing, "Charge", 100m, 120);
        await Ledger(owing, "Charge", 40m, 75);
        await Ledger(owing, "Charge", 50m, 45);
        await Ledger(owing, "Payment", 30m, 10);
        await Ledger(owing, "Charge", 20m, 5);
        await Ledger(prepaid, "Payment", 25m, 10);
        await RunJobAsync(app);

        using var viewer = CreateClient(app, ViewerToken);
        var body = await viewer.GetFromJsonAsync<JsonElement>(ReportPath);

        Assert.Equal("customer-aging.v1", body.GetProperty("reportVersion").GetString());
        Assert.True(body.GetProperty("check").GetProperty("isBalanced").GetBoolean());
        var rows = body.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        var first = rows[0];
        Assert.Equal("Borçlu Müşteri", first.GetProperty("name").GetString());
        AssertAmounts(first, ("days0To30", 20m), ("days31To60", 50m), ("days61To90", 40m), ("over90", 70m), ("totalDebt", 180m), ("unappliedCredit", 0m));
        AssertAmounts(first, ("balance30DaysAgo", 190m), ("balance60DaysAgo", 140m), ("balance90DaysAgo", 100m));
        AssertAmounts(first, ("overdueAmount", 110m));
        Assert.Equal(60, first.GetProperty("paymentTermDays").GetInt32());
        var second = rows[1];
        Assert.Equal("Peşin Ödeyen", second.GetProperty("name").GetString());
        Assert.Equal(25m, second.GetProperty("unappliedCredit").GetDecimal());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("balance30DaysAgo").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("paymentTermDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("overdueAmount").ValueKind);
    }

    [Fact]
    public async Task TheReportFlagsACachedBalanceThatDisagreesWithTheLedger()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd880-tamper");
        await using var app = await StartAsync();
        using var cashierClient = CreateClient(app, null);
        var customer = await CreateCustomerAsync(cashierClient, terminalId, cashier, "Tutarsız Müşteri");
        await Ledger(customer, "Charge", 100m, 5);
        await using (var tamper = _database.DataSource.CreateCommand("UPDATE customer_account.balances SET current_balance = 90 WHERE customer_id = @id;"))
        {
            tamper.Parameters.AddWithValue("id", customer.CustomerId);
            await tamper.ExecuteNonQueryAsync();
        }

        using var viewer = CreateClient(app, ViewerToken);
        var body = await viewer.GetFromJsonAsync<JsonElement>(ReportPath);

        Assert.False(body.GetProperty("check").GetProperty("isBalanced").GetBoolean());
    }

    [Fact]
    public async Task TheSnapshotJobIsRegisteredWithTheMaintenanceRunnerAndASecondRunWritesNothing()
    {
        await using var app = await StartAsync();
        var customer = Guid.NewGuid();
        await Ledger(customer, "Charge", 10m, 3);

        var job = app.Services.GetServices<IMaintenanceJob>().Single(candidate => candidate.Name == "balance-snapshot");
        var first = await RunJobAsync(app);
        var second = await RunJobAsync(app);

        Assert.True(job.DisabledReason is null);
        Assert.Matches(@"^[1-9]\d* gün için [1-9]\d* bakiye kaydı yazıldı\.$", first.Summary);
        Assert.Equal("0 gün için 0 bakiye kaydı yazıldı.", second.Summary);
    }

    [Fact]
    public async Task TheJobWritesTheClosingBalanceOfEachBusinessDayWithTheSixOClockCutoff()
    {
        await using var app = await StartAsync();
        var customer = Guid.NewGuid();
        await Ledger(customer, "Charge", 100m, "2026-11-05T10:00:00+03:00");
        await Ledger(customer, "Charge", 50m, "2026-11-06T05:59:59+03:00");
        await Ledger(customer, "Charge", 7m, "2026-11-06T06:00:00+03:00");
        var job = new BalanceSnapshotMaintenanceJob(new FixedClock(DateTimeOffset.Parse("2026-11-07T12:00:00+03:00", CultureInfo.InvariantCulture)));

        await using (var scope = app.Services.CreateAsyncScope())
            await job.ExecuteAsync(scope.ServiceProvider, CancellationToken.None);

        var balances = await SnapshotBalancesAsync(customer);
        Assert.Equal(150m, balances["2026-11-05"]);
        Assert.Equal(157m, balances["2026-11-06"]);
        Assert.Equal(2, balances.Count);
    }

    private static void AssertAmounts(JsonElement row, params (string Field, decimal Expected)[] expected)
    {
        foreach (var (field, amount) in expected)
            Assert.Equal(amount, row.GetProperty(field).GetDecimal());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private async Task<Dictionary<string, decimal>> SnapshotBalancesAsync(Guid customer)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT to_char(snapshot_date, 'YYYY-MM-DD'), balance FROM customer_account.balance_snapshots WHERE customer_id = @id;");
        command.Parameters.AddWithValue("id", customer);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<string, decimal>();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.GetDecimal(1);
        return result;
    }

    private static async Task<MaintenanceJobOutcome> RunJobAsync(WebApplication app)
    {
        var job = app.Services.GetServices<IMaintenanceJob>().Single(candidate => candidate.Name == "balance-snapshot");
        await using var scope = app.Services.CreateAsyncScope();
        return await job.ExecuteAsync(scope.ServiceProvider, CancellationToken.None);
    }

    private Task Ledger(CustomerAccountSummaryV1 customer, string type, decimal amount, int daysAgo)
        => Ledger(customer.CustomerId, type, amount, daysAgo);

    private Task Ledger(Guid customerId, string type, decimal amount, int daysAgo)
        => InsertLedger(customerId, type, amount, DateTimeOffset.UtcNow.AddDays(-daysAgo));

    private Task Ledger(Guid customerId, string type, decimal amount, string occurredAt)
        => InsertLedger(customerId, type, amount, DateTimeOffset.Parse(occurredAt, CultureInfo.InvariantCulture));

    private async Task InsertLedger(Guid customerId, string type, decimal amount, DateTimeOffset occurredAt)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (gen_random_uuid(), @customer, @type, @amount, 'Test', gen_random_uuid(), @at);
            """);
        command.Parameters.AddWithValue("customer", customerId);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("at", occurredAt.ToUniversalTime());
        await command.ExecuteNonQueryAsync();
    }

    private Task SeedManagerAsync(string rawToken, params string[] permissions)
        => SeedSessionAsync(rawToken, "manager:test", permissions);

    private async Task SeedSessionAsync(string rawToken, string deviceId, params string[] permissions)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var suffix = userId.ToString("N")[..8];
        await using (var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'Yönetici', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Yönetici Test Rolü');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (gen_random_uuid(), @user_id, @role_id);
            """))
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("username", "rmd880-" + suffix);
            command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
            command.Parameters.AddWithValue("device_id", deviceId);
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("role_code", "rmd880-role-" + suffix);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var code in permissions)
        {
            await using var grant = _database.DataSource.CreateCommand(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT gen_random_uuid(), @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """);
            grant.Parameters.AddWithValue("role_id", roleId);
            grant.Parameters.AddWithValue("code", code);
            await grant.ExecuteNonQueryAsync();
        }
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString, _webRoot, "http://127.0.0.1:0", TrustedProxies: [IPAddress.Loopback]));
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app, string? managerToken)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        if (managerToken is not null)
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{ReceivablesAgingEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }

    private static async Task<CustomerAccountSummaryV1> CreateCustomerAsync(HttpClient client, Guid terminalId, string cashierCookie, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/customers")
        {
            Content = JsonContent.Create(new CreateCustomerV1(name, null)),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cashierCookie);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerAccountSummaryV1>())!;
    }
}
