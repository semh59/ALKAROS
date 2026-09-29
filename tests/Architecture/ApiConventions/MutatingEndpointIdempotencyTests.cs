using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Architecture.ApiConventions.Tests;

/// <summary>
/// V1-RMD-430: docs/architecture/api-contract-standard.md makes every data-changing request safe to retry - the client
/// sends an idempotency key and a repeat with the same key must not create a second record. This test reads the real
/// <see cref="DualScreenApplication"/>'s runtime endpoint list and requires every POST, PUT, PATCH and DELETE endpoint
/// to take that key: either an <c>IdempotencyKey</c> property on its JSON request body (what the code uses today) or
/// an <c>X-Idempotency-Key</c> / <c>Idempotency-Key</c> header parameter (what the standard names). Every endpoint
/// that does not today is recorded in idempotency_allowlist.json with its reason and responsible task; the list may
/// only shrink - an entry for an endpoint that now takes the key, or no longer exists, fails the test too.
/// </summary>
[Collection("ApiConventions")]
public sealed class MutatingEndpointIdempotencyTests : IAsyncLifetime
{
    private const string BodyProperty = "IdempotencyKey";

    private static readonly HashSet<string> HeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-Idempotency-Key",
        "Idempotency-Key",
    };

    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    private readonly ApiConventionTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd430-webroot-{Guid.NewGuid():N}");
    private WebApplication? _app;

    static MutatingEndpointIdempotencyTests()
    {
        // Same host configuration as ApiRouteAuthorizationTests: the envelope key and the kitchen station a real host
        // always has.
        Environment.SetEnvironmentVariable(
            "ALKAROS_SECRET_ENVELOPE_MASTER_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Environment.SetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable, "kitchen-main");
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
    public void EveryMutatingEndpointTakesAnIdempotencyKey()
    {
        var allowlist = IdempotencyAllowlist.Load();
        var withoutKey = MutatingEndpoints()
            .Where(endpoint => !TakesIdempotencyKey(endpoint))
            .Select(endpoint => endpoint.Key)
            .ToHashSet(StringComparer.Ordinal);

        var unlisted = withoutKey.Where(key => !allowlist.ContainsKey(key)).Order().ToList();
        var stale = allowlist.Keys.Where(key => !withoutKey.Contains(key)).Order().ToList();

        Assert.True(unlisted.Count == 0,
            "Data-changing endpoints without an idempotency key (add an IdempotencyKey body property or an "
            + "X-Idempotency-Key header, or record the endpoint with a reason and responsible task):\n"
            + string.Join("\n", unlisted));
        Assert.True(stale.Count == 0,
            "Allowlist entries for endpoints that now take an idempotency key or no longer exist (remove them):\n"
            + string.Join("\n", stale));
    }

    [Fact]
    public void EveryAllowlistEntryNamesItsReasonAndResponsibleTask()
    {
        var incomplete = IdempotencyAllowlist.Load()
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value.Reason)
                || pair.Value.Task is null
                || !System.Text.RegularExpressions.Regex.IsMatch(pair.Value.Task, @"^V\d+-[A-Z]+-\d+$"))
            .Select(pair => pair.Key)
            .Order()
            .ToList();

        Assert.True(incomplete.Count == 0, "Allowlist entries without a reason or task ID:\n" + string.Join("\n", incomplete));
    }

    private List<MutatingEndpoint> MutatingEndpoints()
    {
        var dataSource = _app!.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = new SortedDictionary<string, MutatingEndpoint>(StringComparer.Ordinal);
        foreach (var endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
            foreach (var method in methods.Where(MutatingMethods.Contains))
            {
                var mutating = new MutatingEndpoint(
                    method.ToUpperInvariant(),
                    endpoint.RoutePattern.RawText ?? string.Empty,
                    endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType,
                    endpoint.Metadata.GetMetadata<MethodInfo>());
                endpoints.TryAdd(mutating.Key, mutating);
            }
        }

        return endpoints.Values.ToList();
    }

    private static bool TakesIdempotencyKey(MutatingEndpoint endpoint)
    {
        var body = endpoint.BodyType is null ? null : Nullable.GetUnderlyingType(endpoint.BodyType) ?? endpoint.BodyType;
        if (body is not null && body.GetProperty(BodyProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is not null)
            return true;

        return endpoint.Handler?.GetParameters().Any(parameter =>
            parameter.GetCustomAttributes().OfType<IFromHeaderMetadata>().FirstOrDefault() is { } header
            && HeaderNames.Contains(header.Name ?? parameter.Name ?? string.Empty)) ?? false;
    }

    private sealed record MutatingEndpoint(string Method, string Pattern, Type? BodyType, MethodInfo? Handler)
    {
        public string Key => $"{Method} /{Pattern.TrimStart('/')}";
    }
}

/// <summary>Today's data-changing endpoints without an idempotency key. Keys are "METHOD /route/pattern".</summary>
internal static class IdempotencyAllowlist
{
    public static IReadOnlyDictionary<string, AllowlistEntry> Load()
    {
        var path = Path.Combine(ApiConventionTestDatabase.FindRepositoryRoot(),
            "tests", "Architecture", "ApiConventions", "idempotency_allowlist.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var entries = new Dictionary<string, AllowlistEntry>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.GetProperty("withoutIdempotencyKey").EnumerateObject())
        {
            entries.Add(property.Name, new AllowlistEntry(
                property.Value.GetProperty("reason").GetString(),
                property.Value.GetProperty("task").GetString()));
        }

        return entries;
    }
}
