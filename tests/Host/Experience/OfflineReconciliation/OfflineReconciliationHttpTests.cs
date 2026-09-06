using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.OfflineReconciliation;
using ALKAROS.Identity.Authorization.Offline;
using ALKAROS.Identity.DeviceSessions;
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
            RequesterRoleCode: "waiter",
            ReasonCode: "CustomerChange",
            Amount: 40m,
            OfflineAuthorizedAt: issuedAt.AddMinutes(30));

        using var request = JsonRequest(
            Path(terminalId), cookie, new ReconcileOfflineActionsRequest(budget.BudgetId, [action]));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<OfflineReconciliationResultV1>>();
        var result = Assert.Single(results!);
        Assert.Equal(action.IdempotencyKey, result.IdempotencyKey);
        Assert.Equal("Pending", result.Status);
        Assert.Contains("offline_pending_review", result.Detail);
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
            RequesterRoleCode: "waiter",
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

    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionAsync(Guid terminalId)
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
            """,
            ("user_id", userId),
            ("username", "offline-recon-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    public async Task<OfflineAuthorityBudget> SeedOfflineBudgetAsync(
        Guid userId, DateTimeOffset issuedAt, DateTimeOffset expiresAt, params OfflineAuthorityBudgetLine[] lines)
        => await new PostgresOfflineAuthorityBudgetRepository(DataSource)
            .CreateAsync(userId, Guid.NewGuid(), lines, issuedAt, expiresAt);

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
