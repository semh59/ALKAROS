using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.Roles;
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

namespace ALKAROS.Host.Experience.Roles.Tests;

[Collection("Role management PostgreSQL HTTP")]
public sealed class RoleManagementHttpTests : IAsyncLifetime
{
    private readonly RoleManagementTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnonymousAndUnrecognizedSessionsAreRejected()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var anonymous = await client.PostAsJsonAsync(
            RoleManagementEndpoints.GroupPrefix + "/roles", new CreateRoleRequestV1("host", "Host"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task ActorWithoutRolesManagePermissionIsForbiddenFromCreatingARole()
    {
        var cookie = await _database.SeedManagerSessionAsync(withRolesManage: false, withPermissionsManage: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + "/roles", cookie,
            new CreateRoleRequestV1("host", "Host"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<RoleManagementErrorEnvelopeV1>();
        Assert.Equal("FORBIDDEN", error!.Error.Code);
    }

    [Fact]
    public async Task ManagerCanCreateARoleAddAPermissionAndAssignItToTheRole()
    {
        var cookie = await _database.SeedManagerSessionAsync(withRolesManage: true, withPermissionsManage: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var roleCode = "host-" + Guid.NewGuid().ToString("N")[..8];
        var permissionCode = "reservations.manage-" + Guid.NewGuid().ToString("N")[..8];

        using (var createRole = await client.SendAsync(Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + "/roles", cookie,
            new CreateRoleRequestV1(roleCode, "Host"))))
            Assert.Equal(HttpStatusCode.NoContent, createRole.StatusCode);

        using (var duplicateRole = await client.SendAsync(Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + "/roles", cookie,
            new CreateRoleRequestV1(roleCode, "Host"))))
            Assert.Equal(HttpStatusCode.Conflict, duplicateRole.StatusCode);

        using (var addPermission = await client.SendAsync(Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + "/permissions", cookie,
            new AddPermissionRequestV1(permissionCode, "Manage reservations"))))
            Assert.Equal(HttpStatusCode.NoContent, addPermission.StatusCode);

        var roleId = await _database.GetRoleIdAsync(roleCode);

        using (var assign = await client.SendAsync(Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + $"/roles/{roleId:D}/permissions", cookie,
            new AssignPermissionRequestV1(permissionCode))))
            Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        Assert.True(await _database.RoleHasPermissionAsync(roleId, permissionCode));

        using (var revoke = await client.SendAsync(Request(
            HttpMethod.Delete, RoleManagementEndpoints.GroupPrefix + $"/roles/{roleId:D}/permissions/{permissionCode}", cookie)))
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        Assert.False(await _database.RoleHasPermissionAsync(roleId, permissionCode));
    }

    [Fact]
    public async Task ManagerCanAssignAndRevokeAUserFromARole()
    {
        var cookie = await _database.SeedManagerSessionAsync(withRolesManage: true, withPermissionsManage: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var roleCode = "waiter-" + Guid.NewGuid().ToString("N")[..8];

        using (var createRole = await client.SendAsync(Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + "/roles", cookie,
            new CreateRoleRequestV1(roleCode, "Waiter"))))
            Assert.Equal(HttpStatusCode.NoContent, createRole.StatusCode);
        var roleId = await _database.GetRoleIdAsync(roleCode);
        var userId = await _database.SeedPlainUserAsync();

        using (var assign = await client.SendAsync(Request(
            HttpMethod.Post, RoleManagementEndpoints.GroupPrefix + $"/roles/{roleId:D}/users/{userId:D}", cookie)))
            Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        Assert.True(await _database.UserHasRoleAsync(userId, roleId));

        using (var revoke = await client.SendAsync(Request(
            HttpMethod.Delete, RoleManagementEndpoints.GroupPrefix + $"/roles/{roleId:D}/users/{userId:D}", cookie)))
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        Assert.False(await _database.UserHasRoleAsync(userId, roleId));
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddRoleManagementExperience();
        var app = builder.Build();
        app.MapRoleManagementApi();
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (body is not null)
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

public sealed class RoleManagementRegistrationTests
{
    [Fact]
    public void RegistrationPublishesTheRoleManagementRoutes()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=not_opened");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddRoleManagementExperience();
        using var app = builder.Build();
        app.MapRoleManagementApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
            })
            .ToList();
        var prefix = RoleManagementEndpoints.GroupPrefix;
        AssertRoute(routes, "POST", prefix + "/permissions");
        AssertRoute(routes, "POST", prefix + "/roles");
        AssertRoute(routes, "POST", prefix + "/roles/{roleId:guid}/permissions");
        AssertRoute(routes, "DELETE", prefix + "/roles/{roleId:guid}/permissions/{permissionCode}");
        AssertRoute(routes, "POST", prefix + "/roles/{roleId:guid}/users/{userId:guid}");
        AssertRoute(routes, "DELETE", prefix + "/roles/{roleId:guid}/users/{userId:guid}");
    }

    private static void AssertRoute(IEnumerable<dynamic> routes, string method, string pattern)
    {
        Assert.Contains(routes, route =>
            string.Equals(((string?)route.Route)?.TrimEnd('/'), pattern.TrimEnd('/'), StringComparison.Ordinal)
            && ((IReadOnlyList<string>)route.Methods).Contains(method, StringComparer.Ordinal));
    }
}

[CollectionDefinition("Role management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class RoleManagementPostgresqlDefinition;

internal sealed class RoleManagementTestDatabase
{
    private readonly string _databaseName = "alkaros_roles_http_" + Guid.NewGuid().ToString("N")[..8];
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

    public async Task<string> SeedManagerSessionAsync(bool withRolesManage, bool withPermissionsManage)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Role Management Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "role-mgmt-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"manager:{Guid.NewGuid():D}"),
            ("token_hash", hash));

        var codes = new List<string>();
        if (withRolesManage)
            codes.Add("identity.roles.manage");
        if (withPermissionsManage)
            codes.Add("identity.permissions.manage");

        if (codes.Count > 0)
        {
            var roleId = Guid.NewGuid();
            await ExecuteAsync(
                DataSource,
                "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Role Mgmt Test Role');",
                ("role_id", roleId),
                ("role_code", "role-mgmt-role-" + suffix));
            foreach (var code in codes)
            {
                await ExecuteAsync(
                    DataSource,
                    """
                    INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                    SELECT @role_permission_id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                    """,
                    ("role_permission_id", Guid.NewGuid()),
                    ("role_id", roleId),
                    ("code", code));
            }
            await ExecuteAsync(
                DataSource,
                "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@user_role_id, @user_id, @role_id);",
                ("user_role_id", Guid.NewGuid()),
                ("user_id", userId),
                ("role_id", roleId));
        }

        return $"{CatalogManagementEndpoints.ManagerCookieName}={raw}";
    }

    public async Task<Guid> SeedPlainUserAsync()
    {
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            DataSource,
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Plain User', true);
            """,
            ("user_id", userId),
            ("username", "plain-" + userId.ToString("N")));
        return userId;
    }

    public async Task<Guid> GetRoleIdAsync(string code)
    {
        await using var command = DataSource.CreateCommand("SELECT role_id FROM identity.roles WHERE code = @code;");
        command.Parameters.AddWithValue("code", code);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    public async Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT 1 FROM identity.role_permissions rp
            JOIN identity.permissions p ON p.permission_id = rp.permission_id
            WHERE rp.role_id = @role_id AND p.code = @code;
            """);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("code", permissionCode);
        return await command.ExecuteScalarAsync() is not null;
    }

    public async Task<bool> UserHasRoleAsync(Guid userId, Guid roleId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT 1 FROM identity.user_roles WHERE user_id = @user_id AND role_id = @role_id;");
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_id", roleId);
        return await command.ExecuteScalarAsync() is not null;
    }

    private async Task ApplyMigrationsAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations", "V1");
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
