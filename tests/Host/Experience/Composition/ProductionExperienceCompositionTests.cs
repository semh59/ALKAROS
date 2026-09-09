using System.Net;
using System.Net.Sockets;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.DualScreen;
using ALKAROS.ModuleComposition;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Composition.Tests;

public sealed class ProductionExperienceCompositionTests
{
    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task CustomerDisplayOriginOnlyExposesTheDisplayRoutesAndTheMainOriginRefusesThem()
    {
        // deep-analysis finding B-4: with --customer-display-urls the display is
        // served from its own port so the browser partitions its localStorage
        // from the cashier's. Route gating is the enforced half of that split.
        var mainPort = FreeLoopbackPort();
        var displayPort = FreeLoopbackPort();
        var options = new DualScreenOptions(
            "Host=127.0.0.1;Port=5432;Database=alkaros;Username=alkaros;Password=not-used",
            BuildWebRoot(out var webRoot),
            $"http://127.0.0.1:{mainPort}",
            TrustedProxies: [IPAddress.Loopback],
            CustomerDisplayUrl: $"http://127.0.0.1:{displayPort}");
        var display = Guid.NewGuid().ToString("D");

        try
        {
            await using var app = DualScreenApplication.Build(options);
            await app.StartAsync();
            using var client = new HttpClient();

            // Main origin: the display API is not served here.
            Assert.Equal(
                HttpStatusCode.NotFound,
                await GetAsync(client, mainPort, $"/api/v1/customer-displays/{display}/snapshot"));
            // Main origin still serves the cashier API (401 = reached the endpoint).
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                await GetAsync(client, mainPort, $"/api/v1/terminals/{display}/catalog"));

            // Display origin: the cashier API is not served here.
            Assert.Equal(
                HttpStatusCode.NotFound,
                await GetAsync(client, displayPort, $"/api/v1/terminals/{display}/catalog"));
            // Display origin serves the display API (401 = reached the endpoint).
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                await GetAsync(client, displayPort, $"/api/v1/customer-displays/{display}/snapshot"));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    /// <summary>
    /// V1-RMD-139: found by an independent audit (2026-09-09) — the NFC
    /// customer ordering page shared the exact same origin as the cashier
    /// API, so an anonymous customer's phone could reach every non-NFC
    /// route too. Mirrors
    /// CustomerDisplayOriginOnlyExposesTheDisplayRoutesAndTheMainOriginRefusesThem
    /// exactly, for the NFC origin instead.
    /// </summary>
    [Fact]
    public async Task NfcOriginOnlyExposesTheNfcRoutesAndTheMainOriginRefusesThem()
    {
        var mainPort = FreeLoopbackPort();
        var nfcPort = FreeLoopbackPort();
        var options = new DualScreenOptions(
            "Host=127.0.0.1;Port=5432;Database=alkaros;Username=alkaros;Password=not-used",
            BuildWebRoot(out var webRoot),
            $"http://127.0.0.1:{mainPort}",
            TrustedProxies: [IPAddress.Loopback],
            NfcOriginUrl: $"http://127.0.0.1:{nfcPort}");
        var tableId = Guid.NewGuid().ToString("D");

        try
        {
            await using var app = DualScreenApplication.Build(options);
            await app.StartAsync();
            using var client = new HttpClient();

            // Main origin: the NFC API is not served here.
            Assert.Equal(
                HttpStatusCode.NotFound,
                await GetAsync(client, mainPort, $"/api/v1/nfc/tables/{tableId}/catalog"));
            // Main origin still serves the cashier API (401 = reached the endpoint).
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                await GetAsync(client, mainPort, $"/api/v1/terminals/{tableId}/catalog"));

            // NFC origin: the cashier API is not served here.
            Assert.Equal(
                HttpStatusCode.NotFound,
                await GetAsync(client, nfcPort, $"/api/v1/terminals/{tableId}/catalog"));
            // NFC origin serves the NFC API. The catalog endpoint is
            // anonymous by design (no auth check to fail before reaching
            // it) and this test's connection string is a placeholder no
            // real Postgres answers, so the actual outcome is whatever
            // NfcOrderingExceptionFilter maps a database failure to — not
            // NotFound, which is the only status the origin gate itself
            // ever produces. That absence is what proves the gate let the
            // request through to the real endpoint.
            Assert.NotEqual(
                HttpStatusCode.NotFound,
                await GetAsync(client, nfcPort, $"/api/v1/nfc/tables/{tableId}/catalog"));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, int port, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}{path}");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "127.0.0.1");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static string BuildWebRoot(out string webRoot)
    {
        webRoot = Path.Combine(Path.GetTempPath(), "alkaros-composition-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>test</title>");
        return webRoot;
    }

    private static DualScreenOptions BuildOptions(out string webRoot)
    {
        webRoot = Path.Combine(Path.GetTempPath(), "alkaros-composition-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>test</title>");
        return new DualScreenOptions(
            "Host=127.0.0.1;Port=5432;Database=alkaros;Username=alkaros;Password=not-used",
            webRoot,
            "http://127.0.0.1:0",
            AllowInsecureLoopbackDevelopment: true);
    }

    [Fact]
    public void ServeContainerResolvesEveryModuleServiceFromTheModuleCatalog()
    {
        // deep-analysis finding B-2: the serve host must be composed from
        // ModuleRegistry.DefaultCatalog, not a hand-written registration list
        // that can drift from it. Every module-declared service must resolve
        // from the serve container to the module's own implementation type.
        var options = BuildOptions(out var webRoot);
        try
        {
            using var app = DualScreenApplication.Build(options);
            var moduleServices = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog).Services;

            Assert.NotEmpty(moduleServices);
            using var scope = app.Services.CreateScope();

            // A service type registered by more than one module (e.g.
            // IIntegrationEventConsumer, one implementation per module) is an
            // enumerable: GetService returns only the last one, so those are
            // checked through GetServices instead.
            var multiRegistered = moduleServices
                .Where(d => d.ImplementationInstance is null)
                .GroupBy(d => d.ServiceType)
                .Where(g => g.Select(d => d.ImplementationType).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var descriptor in moduleServices.Where(d => d.ImplementationInstance is null))
            {
                if (multiRegistered.Contains(descriptor.ServiceType))
                {
                    var all = scope.ServiceProvider.GetServices(descriptor.ServiceType);
                    Assert.Contains(all, r => r is not null && r.GetType() == descriptor.ImplementationType);
                    continue;
                }

                var resolved = scope.ServiceProvider.GetService(descriptor.ServiceType);
                Assert.True(
                    resolved is not null,
                    $"Module service {descriptor.ServiceType.Name} did not resolve from the serve container.");
                Assert.IsType(descriptor.ImplementationType, resolved);
            }
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task HostMapsTableCatalogAndKitchenExperienceGroups()
    {
        var options = BuildOptions(out var webRoot);

        try
        {
            await using var app = DualScreenApplication.Build(options);
            await app.StartAsync();
            var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText)
                .Where(pattern => pattern is not null)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("/api/v1/terminals/{terminalId:guid}/table-management/tables", endpoints);
            Assert.Contains("/api/v1/terminals/{terminalId:guid}/orders/table", endpoints);
            Assert.Contains("/api/v1/management/catalog/products", endpoints);
            Assert.Contains("/api/v1/terminals/{terminalId:guid}/kitchen-operations/tickets", endpoints);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    /// <summary>
    /// Bağımsız denetimde bulundu (2026-09-05): "/{terminalId}/orders/{orderId}/submit"
    /// hem DualScreenApplication.Endpoints.cs'te (MapApi) hem
    /// OrderManagementEndpoints.cs'te (aynı grup öneki + "/submit") koşulsuz
    /// map ediliyordu — ikisi de aynı WebApplication üzerinde. Sonuç: her
    /// gerçek istek AmbiguousMatchException ile 500'e düşüyordu; PosTerminal'in
    /// asıl "Sipariş gönder" akışı üretimde hiç çalışmıyordu. ASP.NET Core bu
    /// çakışmayı derleme zamanında değil, ilk eşleştirme denemesinde fırlatır —
    /// bu yüzden bunu yakalayacak tek yol, gerçek bir HTTP isteği veya (burada
    /// yapıldığı gibi) tüm endpoint tablosunun aynı (metot, route şablonu)
    /// çiftini iki kez içermediğini doğrulamaktır. Bu test bu SINIFIN
    /// TAMAMINI kapsar — yalnız bu bir örneği değil, gelecekte eklenecek her
    /// yeni endpoint için de aynı korumayı sağlar.
    /// </summary>
    [Fact]
    public async Task NoTwoEndpointsShareTheSameHttpMethodAndRoutePattern()
    {
        var options = BuildOptions(out var webRoot);

        try
        {
            await using var app = DualScreenApplication.Build(options);
            await app.StartAsync();
            var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.RoutePattern.RawText is not null)
                .ToList();

            var duplicates = endpoints
                .SelectMany(endpoint => endpoint.Metadata
                    .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods
                        .Select(method => (Method: method, Pattern: endpoint.RoutePattern.RawText!))
                    ?? [(Method: "*", Pattern: endpoint.RoutePattern.RawText!)])
                .GroupBy(pair => pair)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.True(
                duplicates.Count == 0,
                "Duplicate (HTTP method, route pattern) registrations found — every request to these " +
                "would throw AmbiguousMatchException: " +
                string.Join(", ", duplicates.Select(d => $"{d.Method} {d.Pattern}")));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }
}
