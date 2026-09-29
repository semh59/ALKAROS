using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.CustomerAccounts;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.CustomerAccounts.Tests;

/// <summary>
/// V1-RMD-442: the till's customer account surface over real HTTP (the actual DualScreenApplication.Build host) and a
/// real PostgreSQL with every migration applied.
/// </summary>
[Collection("CustomerAccounts HTTP")]
public sealed class CustomerAccountsHttpTests : IAsyncLifetime
{
    private readonly CustomerAccountsHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd442-webroot-{Guid.NewGuid():N}");

    static CustomerAccountsHttpTests()
    {
        // Customer contact details are an encrypted envelope (V14-CST-001); the host reads its key like production.
        Environment.SetEnvironmentVariable(
            "ALKAROS_SECRET_ENVELOPE_MASTER_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task ABillWrittenToAnAccountWithinTheLimitClosesAndRaisesTheBalance()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd442-charge-ok");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var customer = await CreateCustomerAsync(client, terminalId, cookie, "Ayşe Yılmaz", "0532 111 22 33");
        await _database.SetCreditTermsAsync(customer.CustomerId, 500m, null);
        var billId = await _database.SeedBillAsync(120m);

        var response = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/account-charge", cookie,
            new AccountChargeRequestV1(customer.CustomerId, 120m, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AccountChargeResultV1>();
        Assert.Equal(120m, result!.ApprovedAmount);
        Assert.True(result.BillClosed);
        Assert.Equal(120m, result.BalanceAfter);
        Assert.Equal("Paid", await _database.ScalarTextAsync("SELECT status FROM billing.bills WHERE bill_id = @id;", billId));
    }

    [Fact]
    public async Task ACustomerWithoutACreditLimitIsRefusedWithTheTurkishReason()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd442-charge-nolimit");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var customer = await CreateCustomerAsync(client, terminalId, cookie, "Limitsiz Müşteri", null);
        var billId = await _database.SeedBillAsync(50m);

        var response = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/account-charge", cookie,
            new AccountChargeRequestV1(customer.CustomerId, 50m, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("CREDIT_DENIED", error.GetProperty("code").GetString());
        Assert.Equal("Müşteri için kredi limiti tanımlanmadığından cari hesaba borç yazılamaz.", error.GetProperty("message").GetString());
        Assert.Equal(0L, await _database.CountPaymentsAsync(billId));
    }

    [Fact]
    public async Task ACashierWithoutPaymentsTakeCannotSeeCustomersOrChargeAnAccount()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "rmd442-no-take", ApplicationPermissions.CashDrawer);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var billId = await _database.SeedBillAsync(50m);

        var list = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/customers", cookie);
        var charge = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/account-charge", cookie,
            new AccountChargeRequestV1(Guid.NewGuid(), 50m, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, charge.StatusCode);
    }

    [Fact]
    public async Task ACashReceiptIssuesAReceiptLowersTheBalanceAndCannotExceedTheDebt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd442-receipt");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var customer = await CreateCustomerAsync(client, terminalId, cookie, "Borçlu Müşteri", null);
        await _database.SetCreditTermsAsync(customer.CustomerId, 500m, 30);
        var billId = await _database.SeedBillAsync(100m);
        await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/account-charge", cookie,
            new AccountChargeRequestV1(customer.CustomerId, 100m, Guid.NewGuid().ToString()));
        var sessionId = await OpenSessionAsync(client, terminalId, cookie);
        var receiptPath = $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/account-receipts";

        var paid = await PostAsync(client, receiptPath, cookie, new AccountReceiptRequestV1(customer.CustomerId, 60m, Guid.NewGuid().ToString()));
        var tooMuch = await PostAsync(client, receiptPath, cookie, new AccountReceiptRequestV1(customer.CustomerId, 50m, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var receipt = await paid.Content.ReadFromJsonAsync<AccountReceiptResultV1>();
        Assert.StartsWith("CT-", receipt!.ReceiptNumber);
        Assert.Equal(40m, receipt.BalanceAfter);
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
        var error = (await tooMuch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("RECEIPT_EXCEEDS_BALANCE", error.GetProperty("code").GetString());
        Assert.Equal("Tahsilat, müşterinin 40,00 TL borcunu aşıyor.", error.GetProperty("message").GetString());

        var statement = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/customers/{customer.CustomerId:D}/statement", cookie);
        var body = await statement.Content.ReadFromJsonAsync<CustomerStatementV1>();
        Assert.Equal(40m, body!.Customer.Balance);
        Assert.Contains(body.Entries, entry => entry.Description == "Hesaba yazılan adisyon" && entry.Amount == 100m);
        Assert.Contains(body.Entries, entry => entry.Description == "Tahsilat" && entry.SignedAmount == -60m);
        Assert.Equal(receipt.ReceiptNumber, Assert.Single(body.Receipts).ReceiptNumber);
    }

    [Fact]
    public async Task AReceiptIntoAnotherTerminalsDrawerIsNotFound()
    {
        var terminalA = Guid.NewGuid();
        var terminalB = Guid.NewGuid();
        var cookieA = await _database.SeedCashierSessionAsync(terminalA, "rmd442-drawer-a");
        var cookieB = await _database.SeedCashierSessionAsync(terminalB, "rmd442-drawer-b");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionA = await OpenSessionAsync(client, terminalA, cookieA);

        var response = await PostAsync(client, $"/api/v1/terminals/{terminalB:D}/cash-sessions/{sessionA:D}/account-receipts", cookieB,
            new AccountReceiptRequestV1(Guid.NewGuid(), 10m, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheCustomerListMasksPhonesAndSearchesByNameOrDigits()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd442-list");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var mehmet = await CreateCustomerAsync(client, terminalId, cookie, "Mehmet Işık", "0555 444 33 22");
        await CreateCustomerAsync(client, terminalId, cookie, "Zeynep Kaya", "0533 000 11 99");

        Assert.Equal("*******3322", mehmet.PhoneMasked);
        var byName = await ListAsync(client, terminalId, cookie, "ışık");
        Assert.Equal(mehmet.CustomerId, Assert.Single(byName).CustomerId);
        var byDigits = await ListAsync(client, terminalId, cookie, "4443");
        Assert.Equal(mehmet.CustomerId, Assert.Single(byDigits).CustomerId);
        var raw = await (await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/customers", cookie)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("0555", raw);
    }

    [Fact]
    public async Task ACustomerWithoutANameIsRefusedInTurkish()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd442-noname");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/customers", cookie, new CreateCustomerV1("  ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("Müşteri adı gerekli (en çok 120 karakter).", error.GetProperty("message").GetString());
    }

    private static async Task<CustomerAccountSummaryV1> CreateCustomerAsync(HttpClient client, Guid terminalId, string cookie, string name, string? phone)
    {
        var response = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/customers", cookie, new CreateCustomerV1(name, phone));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerAccountSummaryV1>())!;
    }

    private static async Task<IReadOnlyList<CustomerAccountSummaryV1>> ListAsync(HttpClient client, Guid terminalId, string cookie, string search)
    {
        var response = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/customers?search={Uri.EscapeDataString(search)}", cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerAccountSummaryV1[]>())!;
    }

    private static async Task<Guid> OpenSessionAsync(HttpClient client, Guid terminalId, string cookie)
    {
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        return (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString,
            _webRoot,
            "http://127.0.0.1:0",
            TrustedProxies: [System.Net.IPAddress.Loopback]));
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string? cookie, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        AddTrustedForwarding(request);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddTrustedForwarding(request);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static void AddTrustedForwarding(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
    }
}


[CollectionDefinition("CustomerAccounts HTTP", DisableParallelization = true)]
public sealed class CustomerAccountsHttpDefinition;

internal sealed class CustomerAccountsHttpTestDatabase
{
    private readonly string _databaseName = "alkaros_rmd442_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    public NpgsqlDataSource DataSource => _dataSource ?? throw new InvalidOperationException("Not initialized.");

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

    /// <summary>
    /// Seeds a real user + a real cashier device session bound to <paramref name="terminalId"/>, and returns the raw
    /// Cookie header value. V1-RMD-400: the user holds <c>cash.drawer</c>, as a real cashier does — every drawer
    /// route now requires it; V1-RMD-401: and <c>payments.take</c>, which the cash tender requires.
    /// </summary>
    public Task<string> SeedCashierSessionAsync(Guid terminalId, string rawToken)
        => SeedCashierSessionWithPermissionsAsync(
            terminalId, rawToken, ApplicationPermissions.CashDrawer, ApplicationPermissions.PaymentsTake);

    /// <summary>
    /// V1-RMD-236: seeds a real user + device session + role + explicit
    /// permission grants bound to <paramref name="terminalId"/> — used for
    /// the cash.session.override authorization checks the plain
    /// <see cref="SeedCashierSessionAsync"/> user intentionally has none of.
    /// </summary>
    public async Task<string> SeedCashierSessionWithPermissionsAsync(
        Guid terminalId, string rawToken, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var suffix = userId.ToString("N")[..8];

        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'Kasiyer', true);

            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');

            INSERT INTO identity.roles (role_id, code, name)
            VALUES (@role_id, @role_code, 'Kasiyer Test Rolu');

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            VALUES (gen_random_uuid(), @user_id, @role_id);
            """))
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("username", "rmd442-role-" + suffix);
            command.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
            command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("role_code", "rmd442-role-" + suffix);
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
            await grant.ExecuteNonQueryAsync();
        }

        return $"alkaros.cashier={rawToken}";
    }

    public async Task<string> ScalarTextAsync(string sql, Guid id)
    {
        await using var command = DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> CountPaymentsAsync(Guid billId)
    {
        await using var command = DataSource.CreateCommand("SELECT count(*) FROM payments.payments WHERE bill_id = @id;");
        command.Parameters.AddWithValue("id", billId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task SetCreditTermsAsync(Guid customerId, decimal creditLimit, int? paymentTermDays)
    {
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO customer_account.credit_terms (customer_id, credit_limit, payment_term_days, updated_at, updated_by)
            VALUES (@id, @limit, @term, now(), gen_random_uuid());
            """);
        command.Parameters.AddWithValue("id", customerId);
        command.Parameters.AddWithValue("limit", creditLimit);
        command.Parameters.AddWithValue("term", (object?)paymentTermDays ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Seeds a real, payable Bill (catalog product + table + order + bill) for a cash-tender test.</summary>
    public async Task<Guid> SeedBillAsync(decimal payable)
    {
        var productId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Test Item', 1, 1, @price);
            """))
        {
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
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
            command.Parameters.AddWithValue("table_number", "TBL-" + Guid.NewGuid().ToString("N")[..6]);
            await command.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(), orderId: orderId, productId: productId, productNameSnapshot: "Test Item",
            quantity: 1, unitPrice: payable, taxRate: 0m);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + Guid.NewGuid().ToString("N")[..8], [item], tableId: tableId);
        var orders = new PostgresOrderRepository(DataSource);
        await orders.AddAsync(order);

        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, order.Items[0]);
        var bill = new Bill(
            id: billId,
            billNumber: "BILL-" + Guid.NewGuid().ToString("N")[..8],
            items: [billItem],
            tableId: tableId,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: "TRY");
        var bills = new PostgresBillRepository(DataSource);
        await bills.AddAsync(bill);

        return billId;
    }

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
