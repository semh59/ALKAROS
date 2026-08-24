using System.Text.Json;
using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

public sealed class CustomerDisplayContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ExpectedCatalogProperties =
    [
        "categoryCode", "categoryName", "name", "productId", "sku", "taxRate", "unitPrice",
    ];
    private static readonly string[] ExpectedProperties =
    [
        "currency", "discountTotal", "displayId", "editable", "lines", "message", "orderId", "orderNumber",
        "revision", "serverTimestamp", "state", "subtotal", "taxTotal", "terminalId", "total",
    ];

    [Fact]
    public void SnapshotSerializesOnlyTheCustomerAllowlist()
    {
        var snapshot = new CustomerDisplaySnapshotDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7, "Active", true, "POS-7",
            [new CustomerDisplayLineDto(Guid.NewGuid(), "Çorba", 2, 100, 220)],
            200, 0, 20, 220, "TRY", DateTimeOffset.UtcNow, "Siparişiniz hazırlanıyor.");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, SerializerOptions));
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(ExpectedProperties, properties);
        var serialized = json.RootElement.GetRawText();
        Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogProductCarriesRealCategoryMetadata()
    {
        var product = new CatalogProductDto(
            Guid.NewGuid(), "YMK-001", "Izgara Köfte", "YIYECEK", "Yiyecek", 285, 10);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(product, SerializerOptions));
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(ExpectedCatalogProperties, properties);
    }
}
