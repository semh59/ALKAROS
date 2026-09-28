using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.Authorization;
using ALKAROS.Host.Experience.Catalog;
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

namespace ALKAROS.Host.Experience.Authorization.Tests;

[Collection("Authorization decision PostgreSQL HTTP")]
public sealed class AuthorizationDecisionHttpTests : IAsyncLifetime
{
    private readonly AuthorizationDecisionTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task DecisionSurfaceEnforcesSessionAndPermission()
    {
        var noPermissionCookie = await _database.SeedManagerSessionAsync(withDecisionPermission: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using (var anonymous = await client.GetAsync(AuthorizationDecisionEndpoints.GroupPrefix + "/pending-grants"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var forbiddenRequest = Request(
            HttpMethod.Get, AuthorizationDecisionEndpoints.GroupPrefix + "/pending-grants", noPermissionCookie);
        using var forbidden = await client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task FirstResponderResolvesTheGrantAndTheSecondGetsAConflict()
    {
        var managerOne = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var managerTwo = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var (approverTwoId, _) = _database.LastSeededManager;
        var grantId = await _database.SeedPendingGrantAsync("bills.comp", 120m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // Both managers see the pending request with its full context block.
        var pendingOne = await GetAsync<List<PendingGrantV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/pending-grants", managerOne);
        var pendingTwo = await GetAsync<List<PendingGrantV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/pending-grants", managerTwo);
        Assert.Contains(pendingOne, grant => grant.GrantId == grantId && grant.Amount == 120m);
        Assert.Contains(pendingTwo, grant => grant.GrantId == grantId);

        var resolved = await PostAsync<ResolvedGrantV1>(
            client, GrantPath(grantId, "approve"), managerTwo);
        Assert.Equal("granted", resolved.Status);
        Assert.Equal("manual", resolved.PolicyPath);
        Assert.Equal(approverTwoId, resolved.ApproverUserId);

        using var lateRequest = Request(HttpMethod.Post, GrantPath(grantId, "approve"), managerOne);
        using var late = await client.SendAsync(lateRequest);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        var error = await late.Content.ReadFromJsonAsync<AuthorizationDecisionErrorEnvelopeV1>();
        Assert.Equal("ALREADY_RESOLVED", error!.Error.Code);

        // The resolved grant is gone from the pending list.
        var pendingAfter = await GetAsync<List<PendingGrantV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/pending-grants", managerOne);
        Assert.DoesNotContain(pendingAfter, grant => grant.GrantId == grantId);
    }

    [Fact]
    public async Task DenyRecordsAManualRefusal()
    {
        var manager = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var grantId = await _database.SeedPendingGrantAsync("bills.discount", 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var resolved = await PostAsync<ResolvedGrantV1>(client, GrantPath(grantId, "deny"), manager);

        Assert.Equal("denied", resolved.Status);
        Assert.Equal("manual", resolved.PolicyPath);
    }

    [Fact]
    public async Task DelegationsAndTighteningsCanBeListedRevokedAndCleared()
    {
        var manager = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var delegationId = await _database.SeedActiveDelegationAsync();
        var tighteningId = await _database.SeedOpenTighteningAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var delegations = await GetAsync<List<ActiveDelegationV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/delegations", manager);
        Assert.Contains(delegations, delegation => delegation.DelegationId == delegationId);

        using (var revoke = await client.SendAsync(Request(
            HttpMethod.Post,
            AuthorizationDecisionEndpoints.GroupPrefix + $"/delegations/{delegationId:D}/revoke",
            manager)))
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using (var revokeAgain = await client.SendAsync(Request(
            HttpMethod.Post,
            AuthorizationDecisionEndpoints.GroupPrefix + $"/delegations/{delegationId:D}/revoke",
            manager)))
            Assert.Equal(HttpStatusCode.NotFound, revokeAgain.StatusCode);

        var tightenings = await GetAsync<List<OpenTighteningV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/behavioural-tightenings", manager);
        Assert.Contains(tightenings, tightening => tightening.TighteningId == tighteningId);

        using (var clear = await client.SendAsync(Request(
            HttpMethod.Post,
            AuthorizationDecisionEndpoints.GroupPrefix + $"/behavioural-tightenings/{tighteningId:D}/clear",
            manager)))
            Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        using var clearAgain = await client.SendAsync(Request(
            HttpMethod.Post,
            AuthorizationDecisionEndpoints.GroupPrefix + $"/behavioural-tightenings/{tighteningId:D}/clear",
            manager));
        Assert.Equal(HttpStatusCode.Conflict, clearAgain.StatusCode);
        var error = await clearAgain.Content.ReadFromJsonAsync<AuthorizationDecisionErrorEnvelopeV1>();
        Assert.Equal("ALREADY_CLEARED", error!.Error.Code);
    }

    [Fact]
    public async Task AManagerDelegatesAGrantClassPermissionTheyHoldAndItIsListed()
    {
        // V1-RMD-407 (V1-RMD-399 H-04): the time-boxed hand-off of model §1 can finally be created.
        var manager = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var (managerId, _) = _database.LastSeededManager;
        await _database.GrantToUserRoleAsync(managerId, "bills.comp");
        var granteeId = await _database.SeedGranteeAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(
            AuthorizationDecisionEndpoints.GroupPrefix + "/delegations", manager,
            new CreateDelegationRequestV1(granteeId, "bills.comp", 200m, DateTimeOffset.UtcNow.AddHours(4))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ActiveDelegationV1>();
        Assert.Equal(managerId, created!.DelegatorUserId);
        Assert.Equal(granteeId, created.GranteeUserId);
        var delegations = await GetAsync<List<ActiveDelegationV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/delegations", manager);
        Assert.Contains(delegations, delegation => delegation.DelegationId == created.DelegationId);
    }

    [Fact]
    public async Task ADelegationIsRefusedWhenItBreaksAnyOfItsBounds()
    {
        var manager = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var (managerId, _) = _database.LastSeededManager;
        await _database.GrantToUserRoleAsync(managerId, "bills.comp");
        var granteeId = await _database.SeedGranteeAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var path = AuthorizationDecisionEndpoints.GroupPrefix + "/delegations";
        var soon = DateTimeOffset.UtcNow.AddHours(2);

        async Task<(HttpStatusCode Status, string? Code)> TryAsync(CreateDelegationRequestV1 body)
        {
            using var response = await client.SendAsync(JsonRequest(path, manager, body));
            var error = response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<AuthorizationDecisionErrorEnvelopeV1>();
            return (response.StatusCode, error?.Error.Code);
        }

        Assert.Equal((HttpStatusCode.Forbidden, "DELEGATOR_LACKS_PERMISSION"), await TryAsync(new(granteeId, "bills.void", 50m, soon)));
        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await TryAsync(new(granteeId, "catalog.manage", 0m, soon)));
        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await TryAsync(new(managerId, "bills.comp", 50m, soon)));
        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await TryAsync(new(granteeId, "bills.comp", 50m, DateTimeOffset.UtcNow.AddHours(30))));
        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_FAILED"), await TryAsync(new(granteeId, "bills.comp", -1m, soon)));
        Assert.Equal((HttpStatusCode.NotFound, "GRANTEE_NOT_FOUND"), await TryAsync(new(Guid.NewGuid(), "bills.comp", 50m, soon)));
        Assert.Empty(await GetAsync<List<ActiveDelegationV1>>(client, path, manager));
    }

    /// <summary>
    /// V1-RMD-316 (independent 2026-09-26 audit, finding K9): a user holding both the requester role and
    /// the manager decision permission (explicitly possible in a small establishment, per V1-IAM-020's own
    /// doc comment) must never be able to approve or deny their OWN grant request.
    /// </summary>
    [Fact]
    public async Task ManagerCannotApproveOrDenyTheirOwnGrantRequest()
    {
        var manager = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var (managerId, _) = _database.LastSeededManager;
        var grantId = await _database.SeedPendingGrantAsync("bills.comp", 75m, requesterUserId: managerId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var approve = await client.SendAsync(Request(HttpMethod.Post, GrantPath(grantId, "approve"), manager));
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        var approveError = await approve.Content.ReadFromJsonAsync<AuthorizationDecisionErrorEnvelopeV1>();
        Assert.Equal("SELF_APPROVAL_NOT_ALLOWED", approveError!.Error.Code);

        using var deny = await client.SendAsync(Request(HttpMethod.Post, GrantPath(grantId, "deny"), manager));
        Assert.Equal(HttpStatusCode.Forbidden, deny.StatusCode);

        // The grant is still genuinely pending - neither attempt actually resolved it.
        var pending = await GetAsync<List<PendingGrantV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/pending-grants", manager);
        Assert.Contains(pending, grant => grant.GrantId == grantId);
    }

    /// <summary>
    /// V1-RMD-316 (independent 2026-09-26 audit, finding K9): same self-approval gap, one schema over -
    /// the user whose own behaviour triggered the tightening must never be able to clear it themselves.
    /// </summary>
    [Fact]
    public async Task ManagerCannotClearTheirOwnBehaviouralTightening()
    {
        var manager = await _database.SeedManagerSessionAsync(withDecisionPermission: true);
        var (managerId, _) = _database.LastSeededManager;
        var tighteningId = await _database.SeedOpenTighteningAsync(userId: managerId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var clear = await client.SendAsync(Request(
            HttpMethod.Post,
            AuthorizationDecisionEndpoints.GroupPrefix + $"/behavioural-tightenings/{tighteningId:D}/clear",
            manager));
        Assert.Equal(HttpStatusCode.Forbidden, clear.StatusCode);
        var error = await clear.Content.ReadFromJsonAsync<AuthorizationDecisionErrorEnvelopeV1>();
        Assert.Equal("SELF_APPROVAL_NOT_ALLOWED", error!.Error.Code);

        // Still genuinely open - the attempt did not actually clear it.
        var tightenings = await GetAsync<List<OpenTighteningV1>>(
            client, AuthorizationDecisionEndpoints.GroupPrefix + "/behavioural-tightenings", manager);
        Assert.Contains(tightenings, tightening => tightening.TighteningId == tighteningId);
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddAuthorizationDecisionExperience();
        var app = builder.Build();
        app.MapAuthorizationDecisionApi();
        await app.StartAsync();
        return app;
    }

    private static string GrantPath(Guid grantId, string action)
        => AuthorizationDecisionEndpoints.GroupPrefix + $"/grants/{grantId:D}/{action}";

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path, string cookie)
    {
        using var request = Request(HttpMethod.Get, path, cookie);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, string cookie)
    {
        using var request = Request(HttpMethod.Post, path, cookie);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = Request(HttpMethod.Post, path, cookie);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}

public sealed class AuthorizationDecisionRegistrationTests
{
    [Fact]
    public void RegistrationPublishesTheDecisionRoutes()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddAuthorizationDecisionExperience();
        using var app = builder.Build();
        app.MapAuthorizationDecisionApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();
        var prefix = AuthorizationDecisionEndpoints.GroupPrefix;
        AssertRoute(routes, "GET", prefix + "/pending-grants");
        AssertRoute(routes, "POST", prefix + "/grants/{grantId:guid}/approve");
        AssertRoute(routes, "POST", prefix + "/grants/{grantId:guid}/deny");
        AssertRoute(routes, "GET", prefix + "/delegations");
        AssertRoute(routes, "POST", prefix + "/delegations/{delegationId:guid}/revoke");
        AssertRoute(routes, "GET", prefix + "/behavioural-tightenings");
        AssertRoute(routes, "POST", prefix + "/behavioural-tightenings/{tighteningId:guid}/clear");
    }

    private static void AssertRoute(IEnumerable<dynamic> routes, string method, string pattern)
    {
        Assert.Contains(routes, route =>
            string.Equals(((string?)route.Route)?.TrimEnd('/'), pattern.TrimEnd('/'), StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains(method, StringComparer.Ordinal));
    }
}

[CollectionDefinition("Authorization decision PostgreSQL HTTP", DisableParallelization = true)]
public sealed class AuthorizationDecisionPostgresqlDefinition;

internal sealed class AuthorizationDecisionTestDatabase
{
    private readonly string _databaseName = "alkaros_iam020_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    public (Guid UserId, string Cookie) LastSeededManager { get; private set; }

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

    public async Task<string> SeedManagerSessionAsync(bool withDecisionPermission)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Decision API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "decision-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"manager:{Guid.NewGuid():D}"),
            ("token_hash", hash));

        if (withDecisionPermission)
        {
            await ExecuteAsync(
                DataSource,
                """
                INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Decision Test Role');
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @role_permission_id, @role_id, permission_id FROM identity.permissions WHERE code = 'reports.view';
                INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
                VALUES (@user_role_id, @user_id, @role_id);
                """,
                ("role_id", Guid.NewGuid()),
                ("role_code", "decision-role-" + suffix),
                ("role_permission_id", Guid.NewGuid()),
                ("user_role_id", Guid.NewGuid()),
                ("user_id", userId));
        }

        var cookie = $"{CatalogManagementEndpoints.ManagerCookieName}={raw}";
        LastSeededManager = (userId, cookie);
        return cookie;
    }

    public Task<Guid> SeedPendingGrantAsync(string permissionCode, decimal amount)
        => SeedPendingGrantAsync(permissionCode, amount, requesterUserId: null);

    // V1-RMD-316 (independent 2026-09-26 audit, finding K9): lets a test seed a grant whose requester is a
    // SPECIFIC user (e.g. a manager session's own user id), to prove that user cannot approve/deny it.
    public async Task<Guid> SeedPendingGrantAsync(string permissionCode, decimal amount, Guid? requesterUserId)
    {
        var grantId = Guid.NewGuid();
        var requesterId = requesterUserId ?? await SeedBareUserAsync();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.authorization_grants
                (grant_id, idempotency_key, permission_code, requester_user_id, requester_role_code,
                 amount, reason_code, requested_at, status)
            VALUES (@grant_id, @key, @permission, @requester, 'waiter', @amount, 'CustomerChange', now(), 'pending');
            """,
            ("grant_id", grantId),
            ("key", "decision-" + grantId.ToString("N")),
            ("permission", permissionCode),
            ("requester", requesterId),
            ("amount", amount));
        return grantId;
    }

    public async Task<Guid> SeedActiveDelegationAsync()
    {
        var delegationId = Guid.NewGuid();
        var granteeId = await SeedBareUserAsync();
        var delegatorId = await SeedBareUserAsync();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.authorization_delegations
                (delegation_id, permission_code, grantee_user_id, delegator_user_id, limit_amount, granted_at, expires_at)
            VALUES (@id, 'bills.comp', @grantee, @delegator, 200, now(), now() + interval '2 hours');
            """,
            ("id", delegationId),
            ("grantee", granteeId),
            ("delegator", delegatorId));
        return delegationId;
    }

    public Task<Guid> SeedOpenTighteningAsync() => SeedOpenTighteningAsync(userId: null);

    // V1-RMD-316 (independent 2026-09-26 audit, finding K9): lets a test seed a tightening whose subject
    // is a SPECIFIC user (e.g. a manager session's own user id), to prove that user cannot clear it.
    public async Task<Guid> SeedOpenTighteningAsync(Guid? userId)
    {
        var tighteningId = Guid.NewGuid();
        userId ??= await SeedBareUserAsync();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.behavioural_tightenings
                (tightening_id, user_id, permission_code, recent_count, baseline_per_window, trigger_ratio, triggered_at)
            VALUES (@id, @user, 'bills.void', 6, 1.5, 4, now());
            """,
            ("id", tighteningId),
            ("user", userId));
        return tighteningId;
    }

    // V1-RMD-198: found by the 2026-09-13 CI run - V1-RMD-189 added
    // fk_authorization_grants_requester/fk_authorization_delegations_
    // grantee/delegator/fk_behavioural_tightenings_user (identity.users
    // FKs the whole rest of this codebase already had), and this test
    // project's own seed helpers above had never needed a real user row
    // for these columns before - they inserted a bare Guid.NewGuid() with
    // nothing backing it. A minimal identity.users row (no session, no
    // role - the decision endpoints under test here never look at either)
    // is all any of the three actually need.
    /// <summary>V1-RMD-407: a plain user who can receive a delegation.</summary>
    public Task<Guid> SeedGranteeAsync() => SeedBareUserAsync();

    /// <summary>V1-RMD-407: adds <paramref name="permissionCode"/> to the roles <paramref name="userId"/> already holds.</summary>
    public Task GrantToUserRoleAsync(Guid userId, string permissionCode)
        => ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), ur.role_id, p.permission_id
            FROM identity.user_roles ur, identity.permissions p
            WHERE ur.user_id = @user_id AND p.code = @code;
            """,
            ("user_id", userId),
            ("code", permissionCode));

    private async Task<Guid> SeedBareUserAsync()
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Decision API Seed User', true);
            """,
            ("user_id", userId),
            ("username", "decision-seed-" + userId.ToString("N")));
        return userId;
    }

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
