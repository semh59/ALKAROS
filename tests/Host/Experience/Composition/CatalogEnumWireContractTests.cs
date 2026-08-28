using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ALKAROS.Host.Experience.Composition.Tests;

public sealed class CatalogEnumWireContractTests
{
    [Fact]
    public void HostJsonContractAcceptsLegacyNumbersAndFrontendEnumNames()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "alkaros-composition-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>test</title>");
        var options = new DualScreenOptions(
            "Host=127.0.0.1;Port=5432;Database=alkaros;Username=alkaros;Password=not-used",
            webRoot,
            "http://127.0.0.1:0",
            AllowInsecureLoopbackDevelopment: true);

        try
        {
            using var app = DualScreenApplication.Build(options);
            var serializerOptions = app.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
            var request = JsonSerializer.Deserialize<CreateProductV1>(
                "{\"id\":\"10000000-0000-0000-0000-000000000010\",\"sku\":\"ESP-01\",\"name\":\"Espresso\",\"productType\":\"MenuItem\",\"stockMode\":\"Untracked\"}",
                serializerOptions);
            var legacyRequest = JsonSerializer.Deserialize<CreateProductV1>(
                "{\"id\":\"10000000-0000-0000-0000-000000000011\",\"sku\":\"ESP-02\",\"name\":\"Espresso\",\"productType\":1,\"stockMode\":1}",
                serializerOptions);

            Assert.NotNull(request);
            Assert.Equal(ProductType.MenuItem, request!.ProductType);
            Assert.Equal(StockMode.Untracked, request.StockMode);
            Assert.NotNull(legacyRequest);
            Assert.Equal(ProductType.MenuItem, legacyRequest!.ProductType);
            Assert.Equal(StockMode.Untracked, legacyRequest.StockMode);

            var product = new ProductV1(
                request.Id,
                request.Sku,
                request.Name,
                request.ProductType,
                request.StockMode,
                request.CategoryId,
                request.TaxProfileId,
                request.Description,
                request.PrinterRoutePolicy,
                request.DisplayOrder,
                request.CurrentPrice,
                request.Active);
            var responseJson = JsonSerializer.Serialize(product, serializerOptions);

            Assert.Contains("\"productType\":\"MenuItem\"", responseJson, StringComparison.Ordinal);
            Assert.Contains("\"stockMode\":\"Untracked\"", responseJson, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }
}
