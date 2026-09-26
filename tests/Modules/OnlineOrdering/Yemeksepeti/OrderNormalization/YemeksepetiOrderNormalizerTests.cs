using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization.Tests;

public sealed class NormalizationTestDatabase : PgTestDatabase
{
    public NormalizationTestDatabase() : base("alkaros_ysp_norm_test_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
                 {
                     "006-catalog.up.sql",
                     "040-wave9-schema-additions.up.sql",
                     "053-catalog-products-row-version.up.sql",
                     "103-products-prep-time.up.sql",
                     "144-yemeksepeti-product-mappings.up.sql"
                 })
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));
        }
    }

    public PostgresYemeksepetiProductMappingService Mappings => new(
        DataSource,
        new PostgresProductRepository(DataSource),
        new PostgresProductModifierGroupRepository(DataSource),
        new PostgresModifierGroupRepository(DataSource));

    public YemeksepetiOrderNormalizer CreateNormalizer() =>
        new(Mappings, new PostgresProductRepository(DataSource), new PostgresTaxProfileRepository(DataSource));

    /// <summary>An active product (optionally with a 10% tax profile) mapped from a fresh SKU at 2026-09-01.</summary>
    public async Task<(Guid ProductId, string Sku)> SeedMappedProductAsync(string name = "Kıymalı Pide", bool withTax = true)
    {
        Guid? taxProfileId = null;
        if (withTax)
        {
            taxProfileId = Guid.NewGuid();
            await ExecAsync(
                "INSERT INTO catalog.tax_profiles (tax_profile_id, code, name, vat_rate) VALUES ($1, $2, 'KDV %10', 10);",
                taxProfileId.Value, "KDV-" + taxProfileId.Value.ToString("N")[..8]);
        }

        var productId = Guid.NewGuid();
        await ExecAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, tax_profile_id) VALUES ($1, $2, $3, 1, 1, true, $4);",
            productId, "PD-" + productId.ToString("N")[..8], name, (object?)taxProfileId ?? DBNull.Value);
        var sku = "ys-" + productId.ToString("N")[..10];
        await Mappings.MapAsync(sku, productId, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
        return (productId, sku);
    }

    public Task DeactivateAsync(Guid productId) =>
        ExecAsync("UPDATE catalog.products SET active = false WHERE product_id = $1;", productId);

    private async Task ExecAsync(string sql, params object[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class YemeksepetiOrderNormalizerTests : IClassFixture<NormalizationTestDatabase>
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 25, 18, 0, 0, TimeSpan.Zero);

    private readonly NormalizationTestDatabase _db;
    private readonly YemeksepetiOrderNormalizer _normalizer;

    public YemeksepetiOrderNormalizerTests(NormalizationTestDatabase db)
    {
        _db = db;
        _normalizer = db.CreateNormalizer();
    }

    private static string Item(string sku, string quantity = "2", string price = "145.5", string pricingType = "UNIT") =>
        "{\"_id\":\"i-" + sku + "\",\"sku\":\"" + sku + "\",\"name\":\"provider name\",\"pricing\":{\"pricing_type\":\""
        + pricingType + "\",\"quantity\":" + quantity + ",\"unit_price\":" + price + "}}";

    private static string Order(string items, string extra = "") =>
        $$$"""{"order_id":"5f1c6b8e-0000-4c3d-8e7f-aabbccddeeff","external_order_id":"YS-778899","order_code":"4411","status":"RECEIVED","transport_type":"LOGISTICS_DELIVERY","items":[{{{items}}}]{{{extra}}}}""";

    [Fact]
    public async Task AFullyMappedOrderBecomesInternalLinesWithCatalogNameAndTax()
    {
        var (pide, pideSku) = await _db.SeedMappedProductAsync("Kıymalı Pide");
        var (ayran, ayranSku) = await _db.SeedMappedProductAsync("Ayran");

        var result = await _normalizer.NormalizeAsync(
            Order(Item(pideSku) + "," + Item(ayranSku, "1", "30"), ",\"comment\":\"Acısız olsun\""), ReceivedAt);

        result.Rejection.Should().BeNull();
        var order = result.Order!;
        order.ExternalOrderId.Should().Be("5f1c6b8e-0000-4c3d-8e7f-aabbccddeeff");
        order.DisplayCode.Should().Be("YS-778899");
        order.TransportType.Should().Be("LOGISTICS_DELIVERY");
        order.Comment.Should().Be("Acısız olsun");
        order.Lines.Should().BeEquivalentTo(new[]
        {
            new NormalizedOnlineOrderLine(pideSku, pide, "Kıymalı Pide", 2m, 145.5m, 10m),
            new NormalizedOnlineOrderLine(ayranSku, ayran, "Ayran", 1m, 30m, 10m)
        }, options => options.WithStrictOrdering());
    }

    private static string ItemWith(string sku, string extra) =>
        "{\"_id\":\"i-" + sku + "\",\"sku\":\"" + sku + "\",\"name\":\"provider name\"," + extra
        + "\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":2,\"unit_price\":145.5}}";

    [Fact]
    public async Task ItemInstructionsAreKeptCleanedAndBoundedOnTheLine()
    {
        var (_, sku) = await _db.SeedMappedProductAsync();
        var longNote = new string('x', 250);

        var plain = await _normalizer.NormalizeAsync(Order(ItemWith(sku, "\"instructions\":\" Soğansız\\u0007 \",")), ReceivedAt);
        var bounded = await _normalizer.NormalizeAsync(Order(ItemWith(sku, "\"instructions\":\"" + longNote + "\",")), ReceivedAt);
        var none = await _normalizer.NormalizeAsync(Order(Item(sku)), ReceivedAt);

        plain.Order!.Lines.Single().Instructions.Should().Be("Soğansız");
        bounded.Order!.Lines.Single().Instructions.Should().HaveLength(200);
        none.Order!.Lines.Single().Instructions.Should().BeNull();
    }

    [Theory]
    [InlineData("\"status\":\"IN_CART\",", null)]
    [InlineData("\"status\":null,", null)]
    [InlineData("\"replaced_id\":null,", null)]
    [InlineData("\"replaced_id\":\"\",", null)]
    [InlineData("\"status\":\"REMOVED\",", NormalizationRejection.UnsupportedItemStatus)]
    [InlineData("\"status\":7,", NormalizationRejection.UnsupportedItemStatus)]
    [InlineData("\"replaced_id\":\"9a1b\",", NormalizationRejection.UnsupportedItemStatus)]
    public async Task OnlyTheDocumentedItemStatusBecomesALine(string extra, NormalizationRejection? expected)
    {
        var (_, sku) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(ItemWith(sku, extra)), ReceivedAt);

        result.Rejection.Should().Be(expected);
    }

    [Theory]
    [InlineData("PICKUP")]
    [InlineData("DINE_IN")]
    public async Task ADeliveryKindWithNoDocumentedHandoverIsRefused(string transportType)
    {
        var (_, sku) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(Item(sku)).Replace("LOGISTICS_DELIVERY", transportType), ReceivedAt);

        result.Rejection.Should().Be(NormalizationRejection.UnsupportedTransportType);
        result.Detail.Should().Be(transportType);
    }

    [Theory]
    [InlineData(",\"payment\":{\"sub_total\":291.00}", true)]
    [InlineData(",\"payment\":{\"sub_total\":290.00}", false)]
    [InlineData(",\"payment\":{\"sub_total\":\"291\"}", null)]
    [InlineData("", null)]
    public async Task TheProviderSubTotalIsComparedWithTheLines(string payment, bool? match)
    {
        var (_, sku) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(Item(sku), payment), ReceivedAt);

        result.Order!.LocalSubTotal.Should().Be(291m);
        result.Order.TotalsMatch.Should().Be(match);
    }

    [Fact]
    public async Task OneUnmappedLineRejectsTheWholeOrder()
    {
        var (_, mapped) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(Item(mapped) + "," + Item("ys-unknown")), ReceivedAt);

        result.Order.Should().BeNull();
        result.Rejection.Should().Be(NormalizationRejection.UnmappedSku);
        result.Detail.Should().Be("ys-unknown");
    }

    [Fact]
    public async Task AnOrderPlacedBeforeTheMappingExistedIsUnmapped()
    {
        var (_, sku) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(Item(sku)), new DateTimeOffset(2026, 8, 31, 23, 0, 0, TimeSpan.Zero));

        result.Rejection.Should().Be(NormalizationRejection.UnmappedSku);
    }

    [Fact]
    public async Task AnInactiveOrUntaxedProductIsRejected()
    {
        var (inactive, inactiveSku) = await _db.SeedMappedProductAsync();
        await _db.DeactivateAsync(inactive);
        var (_, untaxedSku) = await _db.SeedMappedProductAsync(withTax: false);

        (await _normalizer.NormalizeAsync(Order(Item(inactiveSku)), ReceivedAt)).Rejection
            .Should().Be(NormalizationRejection.ProductInactive);
        (await _normalizer.NormalizeAsync(Order(Item(untaxedSku)), ReceivedAt)).Rejection
            .Should().Be(NormalizationRejection.ProductHasNoTaxProfile);
    }

    [Theory]
    [InlineData("KG", "1", "10", NormalizationRejection.UnsupportedPricingType)]
    [InlineData("UNIT", "0", "10", NormalizationRejection.InvalidQuantity)]
    [InlineData("UNIT", "1.5", "10", NormalizationRejection.InvalidQuantity)]
    [InlineData("UNIT", "1000", "10", NormalizationRejection.InvalidQuantity)]
    [InlineData("UNIT", "-2", "10", NormalizationRejection.InvalidQuantity)]
    [InlineData("UNIT", "1", "-1", NormalizationRejection.InvalidPrice)]
    [InlineData("UNIT", "1", "\"12\"", NormalizationRejection.InvalidPrice)]
    public async Task UnsupportedOrInvalidPricingIsATypedRejection(
        string pricingType, string quantity, string price, NormalizationRejection expected)
    {
        var (_, sku) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(Item(sku, quantity, price, pricingType)), ReceivedAt);

        result.Order.Should().BeNull();
        result.Rejection.Should().Be(expected);
    }

    [Theory]
    [InlineData("not json", NormalizationRejection.MalformedPayload)]
    [InlineData("""{"order_id":"x","transport_type":"VENDOR_DELIVERY"}""", NormalizationRejection.MalformedPayload)]
    [InlineData("""{"order_id":"x","transport_type":"VENDOR_DELIVERY","items":[]}""", NormalizationRejection.EmptyOrder)]
    [InlineData("""{"transport_type":"VENDOR_DELIVERY","items":[{"sku":"a"}]}""", NormalizationRejection.MalformedPayload)]
    [InlineData("""{"order_id":"x","transport_type":"VENDOR_DELIVERY","items":[{"sku":"a"}]}""", NormalizationRejection.MalformedPayload)]
    public async Task AStructurallyBrokenPayloadIsRejected(string payload, NormalizationRejection expected)
    {
        var result = await _normalizer.NormalizeAsync(payload, ReceivedAt);

        result.Order.Should().BeNull();
        result.Rejection.Should().Be(expected);
    }

    [Fact]
    public async Task TheDisplayCodeFallsBackAndTheCommentIsCleanedAndBounded()
    {
        var (_, sku) = await _db.SeedMappedProductAsync();
        var payload = """{"order_id":"abcdef123456","order_code":"77","transport_type":"VENDOR_DELIVERY","comment":"kapı\u0007zili bozuk """
                      + new string('z', 400) + """ ","items":[""" + Item(sku) + "]}";

        var result = await _normalizer.NormalizeAsync(payload, ReceivedAt);

        result.Order!.DisplayCode.Should().Be("77");
        result.Order.Comment!.Length.Should().Be(200);
        result.Order.Comment.Should().StartWith("kapızili bozuk");
    }

    [Fact]
    public async Task MoreThanFiftyLinesIsRefused()
    {
        var (_, sku) = await _db.SeedMappedProductAsync();

        var result = await _normalizer.NormalizeAsync(Order(string.Join(",", Enumerable.Repeat(Item(sku, "1"), 51))), ReceivedAt);

        result.Rejection.Should().Be(NormalizationRejection.MalformedPayload);
    }
}
