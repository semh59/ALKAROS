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
            foreach (var descriptor in moduleServices.Where(d => d.ImplementationInstance is null))
            {
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
}
