using System.Text.Json;
using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

public sealed class CustomerDisplayContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    // V1-RMD-148: modifierGroups joins the catalog contract so a client can
    // offer a product's options at all. It is null for a product that has
    // none, and this list is asserted exactly — a field appearing here
    // without a deliberate change is what this test is for.
    private static readonly string[] ExpectedCatalogProperties =
    [
        // V1-WTR-017: prepTimeMinutes joins the allowlist here too — same
        // "asserted exactly" discipline as modifierGroups above.
        // V1-CUI-010: remainingCount (last-portion badge) joins the same way.
        "allowsHalfPortion", "categoryCode", "categoryName", "modifierGroups", "name", "prepTimeMinutes", "productId", "remainingCount", "sku", "taxRate", "unitPrice",
    ];
    private static readonly string[] ExpectedProperties =
    [
        "acceptsRound", "currency", "discountTotal", "displayId", "editable", "feeTotal", "lines", "message", "orderId", "orderNumber",
        "paidTotal", "remainingTotal", "revision", "serverTimestamp", "state", "subtotal", "taxTotal", "terminalId", "tipTotal", "total",
    ];
    private static readonly string[] ExpectedLineProperties = ["discountAmount", "itemId", "lineTotal", "name", "quantity", "unitPrice"];

    [Fact]
    public void SnapshotSerializesOnlyTheCustomerAllowlist()
    {
        var snapshot = new CustomerDisplaySnapshotDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7, "Active", true, "POS-7",
            [new CustomerDisplayLineDto(Guid.NewGuid(), "Çorba", 2, 100, 220, 0)],
            200, 0, 20, 220, "TRY", DateTimeOffset.UtcNow, "Siparişiniz hazırlanıyor.");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, SerializerOptions));
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(ExpectedProperties, properties);
        var lineProperties = json.RootElement.GetProperty("lines")[0].EnumerateObject()
            .Select(property => property.Name).Order().ToArray();
        Assert.Equal(ExpectedLineProperties, lineProperties);
        var serialized = json.RootElement.GetRawText();
        Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LineIdsReachOnlyASnapshotThatAskedForThem()
    {
        var withIds = new CustomerDisplayLineDto(Guid.NewGuid(), "Çorba", 1, 100, 100, 0, Guid.NewGuid(), [Guid.NewGuid()]);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(withIds, SerializerOptions));
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(["discountAmount", "itemId", "lineTotal", "modifierIds", "name", "productId", "quantity", "unitPrice"], properties);
    }

    [Fact]
    public void ALineNoteIsWrittenOnlyWhenTheLineHasOne()
    {
        var noted = new CustomerDisplayLineDto(Guid.NewGuid(), "Çorba", 1, 100, 100, 0, Guid.NewGuid(), [Guid.NewGuid()], false, "soğansız");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(noted, SerializerOptions));
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(["discountAmount", "itemId", "lineTotal", "modifierIds", "name", "productId", "quantity", "specialInstructions", "unitPrice"], properties);
        Assert.Equal("soğansız", json.RootElement.GetProperty("specialInstructions").GetString());
    }

    [Fact]
    public void LineOptionsAreWrittenOnlyWhenTheLineHasSome()
    {
        var plain = new CustomerDisplayLineDto(Guid.NewGuid(), "Çorba", 1, 100, 100, 0, Guid.NewGuid(), [], false, null, null);
        var optioned = plain with { Options = [new LineOptionV1("Az acılı", 0), new LineOptionV1("2× Ekstra peynir", 30)] };

        using var withoutOptions = JsonDocument.Parse(JsonSerializer.Serialize(plain, SerializerOptions));
        using var withOptions = JsonDocument.Parse(JsonSerializer.Serialize(optioned, SerializerOptions));

        Assert.False(withoutOptions.RootElement.TryGetProperty("options", out _));
        var options = withOptions.RootElement.GetProperty("options").EnumerateArray().ToArray();
        Assert.Equal(["Az acılı", "2× Ekstra peynir"], options.Select(option => option.GetProperty("name").GetString()));
        Assert.Equal([0m, 30m], options.Select(option => option.GetProperty("price").GetDecimal()));
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
