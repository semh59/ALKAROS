using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Architecture.ApiConventions.Tests;

/// <summary>
/// V1-RMD-429: docs/architecture/api-contract-standard.md requires every HTTP endpoint the Host publishes to live under
/// <c>/api/v1</c> and to pass an authorization decision. The Host enforces permissions inside handlers and endpoint
/// filters rather than through ASP.NET authorization metadata, so the check is behavioral: the real
/// <see cref="DualScreenApplication"/> is started on a real PostgreSQL, its runtime endpoint list is read, and every
/// endpoint is called with no session. An endpoint that answers anything but 401 or 403 did not decide on the caller
/// first. Every endpoint that breaks either rule today is recorded in api_convention_allowlist.json with its reason
/// and responsible task; the list may only shrink - an entry the Host no longer needs fails the test too.
/// </summary>
[Collection("ApiConventions")]
public sealed class ApiRouteAuthorizationTests : IAsyncLifetime
{
    private const string ApiPrefix = "/api/v1";

    private readonly ApiConventionTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd429-webroot-{Guid.NewGuid():N}");
    private WebApplication? _app;

    static ApiRouteAuthorizationTests()
    {
        // The customer endpoints resolve the PII envelope key like production (V14-CST-001).
        Environment.SetEnvironmentVariable(
            "ALKAROS_SECRET_ENVELOPE_MASTER_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
        _app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString,
            _webRoot,
            "http://127.0.0.1:0",
            TrustedProxies: [IPAddress.Loopback]));
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public void EveryEndpointIsUnderTheVersionedApiPrefix()
    {
        var allowlist = ApiConventionAllowlist.Load();
        var outside = PublishedEndpoints()
            .Where(endpoint => !IsUnderApiPrefix(endpoint.Pattern))
            .Select(endpoint => endpoint.Key)
            .ToHashSet(StringComparer.Ordinal);

        var unlisted = outside.Where(key => !allowlist.OutsidePrefix.ContainsKey(key)).Order().ToList();
        var stale = allowlist.OutsidePrefix.Keys.Where(key => !outside.Contains(key)).Order().ToList();

        Assert.True(unlisted.Count == 0,
            "Endpoints outside " + ApiPrefix + " (move them under the prefix or record them with a reason):\n"
            + string.Join("\n", unlisted));
        Assert.True(stale.Count == 0,
            "Allowlist entries for endpoints that are no longer outside " + ApiPrefix + " (remove them):\n"
            + string.Join("\n", stale));
    }

    [Fact]
    public async Task EveryEndpointRefusesACallerWithoutASession()
    {
        var allowlist = ApiConventionAllowlist.Load();
        using var client = CreateClient();
        var answeredWithoutDecision = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var endpoint in PublishedEndpoints())
        {
            var status = await CallWithoutSessionAsync(client, endpoint);
            if (status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
                answeredWithoutDecision[endpoint.Key] = (int)status;
        }

        var unlisted = answeredWithoutDecision
            .Where(pair => !allowlist.WithoutSession.ContainsKey(pair.Key))
            .Select(pair => $"{pair.Key} -> {pair.Value}")
            .Order()
            .ToList();
        var stale = allowlist.WithoutSession.Keys.Where(key => !answeredWithoutDecision.ContainsKey(key)).Order().ToList();

        Assert.True(unlisted.Count == 0,
            "Endpoints that answered a caller without a session with something other than 401/403 (add the "
            + "authorization check, or record a deliberately public endpoint with a reason):\n" + string.Join("\n", unlisted));
        Assert.True(stale.Count == 0,
            "Allowlist entries for endpoints that now refuse a caller without a session (remove them):\n"
            + string.Join("\n", stale));
    }

    [Fact]
    public void EveryAllowlistEntryNamesItsReasonAndResponsibleTask()
    {
        var allowlist = ApiConventionAllowlist.Load();
        var incomplete = allowlist.OutsidePrefix.Concat(allowlist.WithoutSession)
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value.Reason)
                || !System.Text.RegularExpressions.Regex.IsMatch(pair.Value.Task ?? string.Empty, @"^V\d+-[A-Z]+-\d+$"))
            .Select(pair => pair.Key)
            .ToList();

        Assert.True(incomplete.Count == 0, "Allowlist entries without a reason or task ID:\n" + string.Join("\n", incomplete));
    }

    private List<PublishedEndpoint> PublishedEndpoints()
    {
        var dataSource = _app!.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = new SortedDictionary<string, PublishedEndpoint>(StringComparer.Ordinal);
        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            if (methods is null || methods.Count == 0)
                methods = ["GET"];
            foreach (var method in methods)
            {
                var published = new PublishedEndpoint(method, endpoint.RoutePattern, endpoint.Metadata.GetMetadata<MethodInfo>());
                endpoints.TryAdd(published.Key, published);
            }
        }

        return endpoints.Values.ToList();
    }

    private static bool IsUnderApiPrefix(RoutePattern pattern)
    {
        var raw = "/" + (pattern.RawText ?? string.Empty).TrimStart('/');
        return raw.Equals(ApiPrefix, StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith(ApiPrefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpStatusCode> CallWithoutSessionAsync(HttpClient client, PublishedEndpoint endpoint)
    {
        using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.SamplePath() + endpoint.SampleQuery());
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        var fileParameter = endpoint.FileParameterName();
        if (fileParameter is not null)
        {
            // An IFormFile is bound before any endpoint filter runs; a real upload is sent so the request reaches the
            // authorization decision instead of stopping at model binding.
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, fileParameter, "sample.png");
            request.Content = form;
        }
        else if (endpoint.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        return response.StatusCode;
    }

    private HttpClient CreateClient()
    {
        var server = _app!.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            BaseAddress = new Uri(address),
        };
    }

    private sealed record PublishedEndpoint(string Method, RoutePattern Pattern, MethodInfo? Handler)
    {
        private static readonly HashSet<Type> QueryValueTypes =
        [
            typeof(string), typeof(bool), typeof(int), typeof(long), typeof(decimal), typeof(Guid),
            typeof(DateOnly), typeof(DateTime), typeof(DateTimeOffset),
        ];

        public string Key => $"{Method} /{(Pattern.RawText ?? string.Empty).TrimStart('/')}";

        /// <summary>
        /// Required simple handler parameters that are not route values bind from the query string; without them the
        /// request stops at binding (400) and never reaches the handler's authorization decision.
        /// </summary>
        public string SampleQuery()
        {
            if (Handler is null)
                return string.Empty;
            var routeNames = Pattern.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pairs = new List<string>();
            foreach (var parameter in Handler.GetParameters())
            {
                var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
                if (parameter.Name is null || routeNames.Contains(parameter.Name) || !QueryValueTypes.Contains(type)
                    || parameter.GetCustomAttributes().Any(attribute => attribute.GetType().Name is "FromServicesAttribute"
                        or "FromBodyAttribute" or "FromHeaderAttribute" or "FromFormAttribute"))
                    continue;
                pairs.Add($"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(SampleQueryValue(type))}");
            }

            return pairs.Count == 0 ? string.Empty : "?" + string.Join("&", pairs);
        }

        public string? FileParameterName()
            => Handler?.GetParameters()
                .FirstOrDefault(parameter => parameter.ParameterType == typeof(IFormFile))
                ?.Name;

        private static string SampleQueryValue(Type type)
            => type == typeof(Guid) ? "5b0c7c1e-0000-4000-8000-000000000429"
                : type == typeof(bool) ? "true"
                : type == typeof(int) || type == typeof(long) || type == typeof(decimal) ? "1"
                : type == typeof(DateOnly) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ? "2026-09-29"
                : "sample";

        /// <summary>
        /// A concrete path for the pattern: each parameter gets a value its route constraint accepts, so the request
        /// reaches the endpoint instead of stopping at routing.
        /// </summary>
        public string SamplePath()
        {
            var builder = new StringBuilder();
            foreach (var segment in Pattern.PathSegments)
            {
                builder.Append('/');
                foreach (var part in segment.Parts)
                {
                    builder.Append(part switch
                    {
                        RoutePatternLiteralPart literal => literal.Content,
                        RoutePatternSeparatorPart separator => separator.Content,
                        RoutePatternParameterPart parameter => SampleValue(parameter),
                        _ => string.Empty,
                    });
                }
            }

            return builder.Length == 0 ? "/" : builder.ToString();
        }

        private static string SampleValue(RoutePatternParameterPart parameter)
        {
            var constraints = parameter.ParameterPolicies.Select(policy => policy.Content ?? string.Empty).ToList();
            if (constraints.Any(c => c.StartsWith("guid", StringComparison.OrdinalIgnoreCase)))
                return "5b0c7c1e-0000-4000-8000-000000000429";
            if (constraints.Any(c => c.StartsWith("int", StringComparison.OrdinalIgnoreCase)
                || c.StartsWith("long", StringComparison.OrdinalIgnoreCase)))
                return "1";
            if (constraints.Any(c => c.StartsWith("datetime", StringComparison.OrdinalIgnoreCase)))
                return "2026-09-29";
            if (constraints.Any(c => c.StartsWith("bool", StringComparison.OrdinalIgnoreCase)))
                return "true";
            if (parameter.Name.Contains("date", StringComparison.OrdinalIgnoreCase))
                return "2026-09-29";
            if (parameter.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase))
                return "5b0c7c1e-0000-4000-8000-000000000429";
            return "sample";
        }
    }
}

/// <summary>
/// The ratchet list of today's known exceptions. Keys are "METHOD /route/pattern" exactly as the endpoint list reports
/// them.
/// </summary>
internal sealed class ApiConventionAllowlist
{
    public required IReadOnlyDictionary<string, AllowlistEntry> OutsidePrefix { get; init; }

    public required IReadOnlyDictionary<string, AllowlistEntry> WithoutSession { get; init; }

    public static ApiConventionAllowlist Load()
    {
        var path = Path.Combine(ApiConventionTestDatabase.FindRepositoryRoot(),
            "tests", "Architecture", "ApiConventions", "api_convention_allowlist.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return new ApiConventionAllowlist
        {
            OutsidePrefix = Read(document.RootElement.GetProperty("outsideApiPrefix")),
            WithoutSession = Read(document.RootElement.GetProperty("answersWithoutSession")),
        };
    }

    private static Dictionary<string, AllowlistEntry> Read(JsonElement section)
    {
        var entries = new Dictionary<string, AllowlistEntry>(StringComparer.Ordinal);
        foreach (var property in section.EnumerateObject())
        {
            entries.Add(property.Name, new AllowlistEntry(
                property.Value.GetProperty("reason").GetString(),
                property.Value.GetProperty("task").GetString()));
        }

        return entries;
    }
}

internal sealed record AllowlistEntry(string? Reason, string? Task);

[CollectionDefinition("ApiConventions", DisableParallelization = true)]
public sealed class ApiConventionsDefinition;

internal sealed class ApiConventionTestDatabase
{
    private readonly string _databaseName = "alkaros_rmd429_" + Guid.NewGuid().ToString("N")[..8];

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
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString() ?? throw new InvalidOperationException("Migration ID is missing.");
            var files = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories);
            await ExecuteAsync(dataSource, await File.ReadAllTextAsync(files[0]));
        }
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return;
        NpgsqlConnection.ClearAllPools();
        var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
        await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
    }

    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}
