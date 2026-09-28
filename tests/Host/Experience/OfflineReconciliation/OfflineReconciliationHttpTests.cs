using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.OfflineReconciliation;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Offline;
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

namespace ALKAROS.Host.Experience.OfflineReconciliation.Tests;

/// <summary>
/// V1-IAM-025 (C3): the reconnect endpoint over the real repositories and
/// database — the module-level engine behaviour (budget headroom, expiry,
/// live-policy re-check, idempotent replay) is already covered by
/// OfflineGrantReconcilerTests; this exercises the HTTP contract wrapped
/// around it (session auth, request/response mapping, error envelope).
/// </summary>
[Collection("Offline reconciliation PostgreSQL HTTP")]
public sealed class OfflineReconciliationHttpTests : IAsyncLifetime
{
    private readonly OfflineReconciliationTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            Path(terminalId), new ReconcileOfflineActionsRequest(Guid.NewGuid(), []));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownBudgetIsRejectedWithNotFound()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(Guid.NewGuid(), []));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OfflineReconciliationErrorV1>();
        Assert.Equal("UNKNOWN_BUDGET", body!.Code);
    }

    [Fact]
    public async Task AWithinBudgetActionIsReconciledPendingForManagerReview()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var budget = await _database.SeedOfflineBudgetAsync(
            userId, issuedAt, issuedAt.AddHours(4),
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var action = new OfflineAuthorizedActionV1(
            IdempotencyKey: "http-recon-" + Guid.NewGuid().ToString("N"),
            PermissionCode: "bills.comp",
            RequesterUserId: userId,
            RequesterRoleCode: "cashier",
            ReasonCode: "CustomerChange",
            Amount: 40m,
            OfflineAuthorizedAt: issuedAt.AddMinutes(30));

        using var request = JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [action]));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OfflineReconciliationResponseV1>();
        var result = Assert.Single(body!.Results);
        Assert.Equal(action.IdempotencyKey, result.IdempotencyKey);
        Assert.Equal("Pending", result.Status);
        Assert.Contains("offline_pending_review", result.Detail);
        Assert.False(result.IsFlagged);

        Assert.Equal(1, body.Summary.TotalActions);
        Assert.Equal(1, body.Summary.PendingReviewCount);
        Assert.Equal(0, body.Summary.FlaggedCount);
        Assert.Contains("1 çevrimdışı işlem değerlendirildi", body.Summary.Brief);
        Assert.Contains("1 tanesi yönetici onayı bekliyor", body.Summary.Brief);
    }

    [Fact]
    public async Task AFlaggedPendingActionSortsFirstAndTheSummaryCountsIt()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _database.SeedActiveTighteningAsync(userId, "bills.comp", issuedAt);
        var budget = await _database.SeedOfflineBudgetAsync(
            userId, issuedAt, issuedAt.AddHours(4),
            new OfflineAuthorityBudgetLine("bills.void", null, 5),
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // The unflagged action is listed first in the request; the response
        // must still put the flagged one first.
        var unflagged = new OfflineAuthorizedActionV1(
            "http-recon-unflagged-" + Guid.NewGuid().ToString("N"), "bills.void", userId, "cashier",
            "CustomerChange", 0m, issuedAt.AddMinutes(10));
        var flagged = new OfflineAuthorizedActionV1(
            "http-recon-flagged-" + Guid.NewGuid().ToString("N"), "bills.comp", userId, "cashier",
            "CustomerChange", 40m, issuedAt.AddMinutes(30));

        using var request = JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [unflagged, flagged]));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OfflineReconciliationResponseV1>();
        Assert.Equal(2, body!.Summary.TotalActions);
        Assert.Equal(2, body.Summary.PendingReviewCount);
        Assert.Equal(1, body.Summary.FlaggedCount);
        Assert.Contains("öncelikli", body.Summary.Brief);

        Assert.Equal(2, body.Results.Count);
        Assert.Equal(flagged.IdempotencyKey, body.Results[0].IdempotencyKey);
        Assert.True(body.Results[0].IsFlagged);
        Assert.False(body.Results[1].IsFlagged);
    }

    [Fact]
    public async Task AFlaggedButDeniedActionIsNotClaimedAsPendingInTheBrief()
    {
        // Found by an independent audit (2026-09-15): FlaggedCount used to be
        // computed over every status, but BuildBrief's pending clause reads
        // it as "bunlardan {flagged} tanesi..." — "of these [pending] ones".
        // Flagging never changes Admit/Deny (OfflineGrantReconciler), so a
        // flagged action can end up Denied (e.g. amount over the budget
        // line's limit) instead of Pending, and the old brief would falsely
        // claim the flagged item was in the pending bucket when it had
        // actually been denied.
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _database.SeedActiveTighteningAsync(userId, "bills.comp", issuedAt);
        var budget = await _database.SeedOfflineBudgetAsync(
            userId, issuedAt, issuedAt.AddHours(4),
            new OfflineAuthorityBudgetLine("bills.void", null, 5),
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var pending = new OfflineAuthorizedActionV1(
            "http-recon-pending-" + Guid.NewGuid().ToString("N"), "bills.void", userId, "cashier",
            "CustomerChange", 0m, issuedAt.AddMinutes(10));
        // Flagged (active tightening on bills.comp) but over the budget
        // line's 150m limit, so the reconciler denies it despite the flag.
        var flaggedButDenied = new OfflineAuthorizedActionV1(
            "http-recon-flagged-denied-" + Guid.NewGuid().ToString("N"), "bills.comp", userId, "cashier",
            "CustomerChange", 500m, issuedAt.AddMinutes(30));

        using var request = JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [pending, flaggedButDenied]));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OfflineReconciliationResponseV1>();
        Assert.Equal(2, body!.Summary.TotalActions);
        Assert.Equal(1, body.Summary.PendingReviewCount);
        Assert.Equal(1, body.Summary.DeniedCount);
        Assert.Equal(1, body.Summary.FlaggedCount);

        var deniedResult = Assert.Single(body.Results, r => r.IdempotencyKey == flaggedButDenied.IdempotencyKey);
        Assert.Equal("Denied", deniedResult.Status);
        Assert.True(deniedResult.IsFlagged);

        // The pending clause must not claim a flagged item is among the
        // pending ones when the only flagged item was actually denied.
        Assert.DoesNotContain("öncelikli", body.Summary.Brief);
        Assert.Contains("1 tanesi yönetici onayı bekliyor", body.Summary.Brief);
        Assert.Contains("1 tanesi otomatik reddedildi", body.Summary.Brief);
    }

    [Fact]
    public async Task AnotherAuthenticatedCashierCannotReconcileSomeoneElsesBudget()
    {
        // Found by an independent audit (2026-09-06): the authenticated
        // principal was discarded, so any cashier who learned another
        // employee's budgetId could reconcile offline actions attributed to
        // that employee.
        var terminalId = Guid.NewGuid();
        var (ownerId, _) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, attackerCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var budget = await _database.SeedOfflineBudgetAsync(
            ownerId, issuedAt, issuedAt.AddHours(4),
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var action = new OfflineAuthorizedActionV1(
            IdempotencyKey: "http-recon-mismatch-" + Guid.NewGuid().ToString("N"),
            PermissionCode: "bills.comp",
            RequesterUserId: ownerId,
            RequesterRoleCode: "cashier",
            ReasonCode: "CustomerChange",
            Amount: 40m,
            OfflineAuthorizedAt: issuedAt.AddMinutes(30));

        using var request = JsonRequest(
            Path(terminalId), attackerCookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [action]));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OfflineReconciliationErrorV1>();
        Assert.Equal("IDENTITY_MISMATCH", body!.Code);
    }

    [Fact]
    public async Task AClaimedRoleTheUserDoesNotHoldIsRefused()
    {
        // V1-RMD-404 (V1-RMD-399 H-05): a waiter device claiming "manager" used to dodge a live always_deny policy.
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var budget = await _database.SeedOfflineBudgetAsync(
            userId, issuedAt, issuedAt.AddHours(4), new OfflineAuthorityBudgetLine("bills.void", 150m, 5));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var forged = new OfflineAuthorizedActionV1(
            "http-recon-forged-" + Guid.NewGuid().ToString("N"), "bills.void", userId, "manager",
            "CustomerChange", 20m, issuedAt.AddMinutes(10));
        using var response = await client.SendAsync(JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [forged])));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("IDENTITY_MISMATCH", (await response.Content.ReadFromJsonAsync<OfflineReconciliationErrorV1>())!.Code);
    }

    [Fact]
    public async Task AWaitersOfflineCompIsJudgedAgainstTheOrdersRealServer()
    {
        // V1-RMD-404 (V1-RMD-399 H-06): the device's SubjectServingUserId is ignored; the order says who serves it.
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var (otherWaiterId, _) = await _database.SeedCashierSessionAsync(Guid.NewGuid(), "waiter");
        var otherWaiterItem = await _database.SeedOrderItemServedByAsync(otherWaiterId);
        var ownItem = await _database.SeedOrderItemServedByAsync(userId);
        var issuedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var budget = await _database.SeedOfflineBudgetAsync(
            userId, issuedAt, issuedAt.AddHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var onOthersCheck = new OfflineAuthorizedActionV1(
            "http-recon-others-" + Guid.NewGuid().ToString("N"), "bills.comp", userId, "waiter",
            "CustomerChange", 40m, issuedAt.AddMinutes(10),
            SubjectType: "OrderItem", SubjectId: otherWaiterItem, SubjectServingUserId: userId);
        var onOwnCheck = new OfflineAuthorizedActionV1(
            "http-recon-own-" + Guid.NewGuid().ToString("N"), "bills.comp", userId, "waiter",
            "CustomerChange", 40m, issuedAt.AddMinutes(20),
            SubjectType: "OrderItem", SubjectId: ownItem);
        using var response = await client.SendAsync(JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [onOthersCheck, onOwnCheck])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OfflineReconciliationResponseV1>();
        Assert.Equal("Denied", Assert.Single(body!.Results, r => r.IdempotencyKey == onOthersCheck.IdempotencyKey).Status);
        Assert.Equal("Pending", Assert.Single(body.Results, r => r.IdempotencyKey == onOwnCheck.IdempotencyKey).Status);
    }

    private static string Path(Guid terminalId)
        => OfflineReconciliationEndpoints.RoutePrefix.Replace("{terminalId:guid}", terminalId.ToString("D"));

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddOfflineReconciliationExperience();
        var app = builder.Build();
        app.MapOfflineReconciliationApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}

public sealed class OfflineReconciliationRegistrationTests
{
    [Fact]
    public void RegistrationPublishesTheReconciliationRoute()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddOfflineReconciliationExperience();
        using var app = builder.Build();
        app.MapOfflineReconciliationApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();
        Assert.Contains(routes, route =>
            string.Equals(
                ((string?)route.Route)?.TrimEnd('/'),
                OfflineReconciliationEndpoints.RoutePrefix.TrimEnd('/'),
                StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains("POST", StringComparer.Ordinal));
    }
}

[CollectionDefinition("Offline reconciliation PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OfflineReconciliationPostgresqlDefinition;

internal sealed class OfflineReconciliationTestDatabase
{
    private readonly string _databaseName = "alkaros_iam025_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    private NpgsqlDataSource DataSource
        => _dataSource ?? throw new InvalidOperationException("The test database is not initialized.");

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
        var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
        await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
    }

    /// <summary>
    /// A user in the migration-seeded <paramref name="roleCode"/> role with a cashier device session. V1-RMD-404:
    /// the endpoint now checks every action's role against the caller's real role, so the user needs one.
    /// </summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionAsync(Guid terminalId, string roleCode = "cashier")
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Offline Reconciliation API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), @user_id, role_id FROM identity.roles WHERE code = @role_code;
            """,
            ("user_id", userId),
            ("role_code", roleCode),
            ("username", "offline-recon-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>V1-RMD-404: a real order with one item served by <paramref name="servingUserId"/>; returns the item id.</summary>
    public async Task<Guid> SeedOrderItemServedByAsync(Guid? servingUserId)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Offline Recon Item', 1, 1, 40.00);
            """,
            ("product_id", productId),
            ("sku", "RMD404-" + Guid.NewGuid().ToString("N")[..10]));
        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(), orderId: orderId, productId: productId, productNameSnapshot: "Offline Recon Item",
            quantity: 1, unitPrice: 40.00m, taxRate: 0m);
        await new PostgresOrderRepository(DataSource).AddAsync(new Order(
            orderId, OrderSource.Cashier, "RMD404-" + Guid.NewGuid().ToString("N")[..10], [item],
            servingUserId: servingUserId));
        return item.Id;
    }

    public async Task<OfflineAuthorityBudget> SeedOfflineBudgetAsync(
        Guid userId, DateTimeOffset issuedAt, DateTimeOffset expiresAt, params OfflineAuthorityBudgetLine[] lines)
        => await new PostgresOfflineAuthorityBudgetRepository(DataSource)
            .CreateAsync(userId, Guid.NewGuid(), lines, issuedAt, expiresAt);

    public Task SeedActiveTighteningAsync(Guid userId, string permissionCode, DateTimeOffset triggeredAt)
        => new PostgresBehaviouralTighteningRepository(DataSource).OpenAsync(
            new BehaviouralTightening(
                Guid.Empty, userId, permissionCode, RecentCount: 6, BaselinePerWindow: 2m,
                TriggerRatio: 3m, TriggeredAt: triggeredAt, ClearedAt: null, ClearedByUserId: null));

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
        NpgsqlDataSource dataSource, string sql, params (string Name, object Value)[] parameters)
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
