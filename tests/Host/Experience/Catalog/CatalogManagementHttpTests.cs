using System.Net;
using System.Net.Http.Json;
using ALKAROS.Catalog.Pricing;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Catalog.Tests;

public sealed class CatalogManagementHttpTests : IClassFixture<CatalogApiTestDatabase>, IAsyncLifetime
{
    private readonly CatalogApiTestDatabase _database;
    private WebApplication? _application;
    private Uri? _baseAddress;

    public CatalogManagementHttpTests(CatalogApiTestDatabase database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        // AddCatalogManagement() now registers its own filter dependencies
        // (IRoleRepository/IDenialEventSink/IAuthorizationService) — found
        // missing by an independent audit (2026-09-05); this test used to
        // paper over the gap by registering them here by hand.
        builder.Services.AddCatalogManagement();

        _application = builder.Build();
        _application.MapCatalogManagement();
        await _application.StartAsync();
        var addresses = _application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
    }

    [Fact]
    public async Task MissingAndUnpermissionedSessionsReturnStableErrorsWithoutMutation()
    {
        var code = "AUTH-" + Guid.NewGuid().ToString("N")[..8];
        var request = new CreateCategoryV1(Guid.NewGuid(), code, "Protected category");

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/management/catalog/categories", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(CatalogApiTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync("/api/v1/management/catalog/categories", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);

        Assert.Equal(
            0L,
            await ScalarAsync<long>("SELECT count(*) FROM catalog.categories WHERE code = @value;", code));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                "SELECT count(*) FROM identity.denial_events WHERE user_id = @value;",
                CatalogApiTestDatabase.DeniedUserId));
    }

    [Fact]
    public async Task ManagerCanCreateAndReadEveryCatalogResourceOverHttp()
    {
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryId = Guid.NewGuid();
        var taxProfileId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var modifierGroupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var effectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-10);

        await AssertCreatedAsync(client, "/api/v1/management/catalog/categories",
            new CreateCategoryV1(categoryId, $"CAT-{suffix}", "Meals"));
        await AssertCreatedAsync(client, "/api/v1/management/catalog/tax-profiles",
            new CreateTaxProfileV1(taxProfileId, $"TAX-{suffix}", "Food VAT", 10));
        await AssertCreatedAsync(client, "/api/v1/management/catalog/products",
            new CreateProductV1(
                productId,
                $"SKU-{suffix}",
                "Soup",
                ProductType.MenuItem,
                StockMode.Untracked,
                categoryId,
                taxProfileId));
        await AssertCreatedAsync(client, "/api/v1/management/catalog/modifier-groups",
            new CreateModifierGroupV1(
                modifierGroupId,
                $"GRP-{suffix}",
                "Extras",
                SelectionType.SelectMany,
                0,
                2));
        await AssertCreatedAsync(client, "/api/v1/management/catalog/modifiers",
            new CreateModifierV1(
                modifierId,
                modifierGroupId,
                $"MOD-{suffix}",
                "Croutons",
                5,
                productId));
        await AssertCreatedAsync(client, "/api/v1/management/catalog/product-modifier-assignments",
            new CreateProductModifierAssignmentV1(assignmentId, productId, modifierGroupId));
        await AssertCreatedAsync(client, "/api/v1/management/catalog/prices",
            new CreateProductPriceV1(priceId, productId, PriceType.SalePrice, 125, effectiveFrom));

        Assert.Contains(categoryId, await ReadIdsAsync<CategoryV1>(client, "categories", value => value.Id));
        Assert.Contains(taxProfileId, await ReadIdsAsync<TaxProfileV1>(client, "tax-profiles", value => value.Id));
        Assert.Contains(productId, await ReadIdsAsync<ProductV1>(client, "products", value => value.Id));
        Assert.Contains(modifierGroupId, await ReadIdsAsync<ModifierGroupV1>(client, "modifier-groups", value => value.Id));
        Assert.Contains(modifierId, await ReadIdsAsync<ModifierV1>(client, "modifiers", value => value.Id));
        Assert.Contains(
            assignmentId,
            await ReadIdsAsync<ProductModifierAssignmentV1>(
                client,
                "product-modifier-assignments",
                value => value.Id));
        Assert.Contains(priceId, await ReadIdsAsync<ProductPriceV1>(client, "prices", value => value.Id));

        var at = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        using var effectiveResponse = await client.GetAsync(
            $"/api/v1/management/catalog/effective-price?productId={productId:D}&priceType=1&currencyCode=TRY&at={at}");
        effectiveResponse.EnsureSuccessStatusCode();
        var effective = await effectiveResponse.Content.ReadFromJsonAsync<ProductPriceV1>();
        Assert.NotNull(effective);
        Assert.Equal(priceId, effective.Id);
    }

    [Fact]
    public async Task CursorIsDeterministicResourceBoundAndPageSizeIsBounded()
    {
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        foreach (var ending in new[] { "A", "B", "C" })
        {
            await AssertCreatedAsync(
                client,
                "/api/v1/management/catalog/categories",
                new CreateCategoryV1(Guid.NewGuid(), $"000-{suffix}-{ending}", ending));
        }

        var first = await client.GetFromJsonAsync<CatalogPageV1<CategoryV1>>(
            "/api/v1/management/catalog/categories?limit=2");
        Assert.NotNull(first);
        Assert.Equal(new[] { $"000-{suffix}-A", $"000-{suffix}-B" }, first.Items.Select(item => item.Code));
        Assert.NotNull(first.NextCursor);

        var second = await client.GetFromJsonAsync<CatalogPageV1<CategoryV1>>(
            $"/api/v1/management/catalog/categories?limit=2&cursor={Uri.EscapeDataString(first.NextCursor)}");
        Assert.NotNull(second);
        Assert.Equal($"000-{suffix}-C", second.Items[0].Code);

        using var invalidLimit = await client.GetAsync("/api/v1/management/catalog/categories?limit=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await ReadErrorAsync(invalidLimit)).Error.Code);

        using var wrongResource = await client.GetAsync(
            $"/api/v1/management/catalog/products?limit=2&cursor={Uri.EscapeDataString(first.NextCursor)}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongResource.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await ReadErrorAsync(wrongResource)).Error.Code);
    }

    [Fact]
    public async Task DuplicateNegativeAndOverlappingMutationsAreAtomicAndCorrectable()
    {
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var productId = Guid.NewGuid();
        var sku = $"ATOMIC-{suffix}";
        await AssertCreatedAsync(
            client,
            "/api/v1/management/catalog/products",
            new CreateProductV1(productId, sku, "Atomic", ProductType.MenuItem, StockMode.Untracked));

        using var duplicate = await client.PostAsJsonAsync(
            "/api/v1/management/catalog/products",
            new CreateProductV1(Guid.NewGuid(), sku, "Duplicate", ProductType.MenuItem, StockMode.Untracked));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("DUPLICATE_SKU", (await ReadErrorAsync(duplicate)).Error.Code);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM catalog.products WHERE sku = @value;", sku));

        var failedPriceId = Guid.NewGuid();
        using var negative = await client.PostAsJsonAsync(
            "/api/v1/management/catalog/prices",
            new CreateProductPriceV1(
                failedPriceId,
                productId,
                PriceType.SalePrice,
                -1,
                DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await ReadErrorAsync(negative)).Error.Code);
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                "SELECT count(*) FROM catalog.product_prices WHERE product_price_id = @value;",
                failedPriceId));

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        await AssertCreatedAsync(
            client,
            "/api/v1/management/catalog/prices",
            new CreateProductPriceV1(Guid.NewGuid(), productId, PriceType.SalePrice, 100, start, "TRY", start.AddHours(2)));

        var correctedPriceId = Guid.NewGuid();
        using var overlap = await client.PostAsJsonAsync(
            "/api/v1/management/catalog/prices",
            new CreateProductPriceV1(
                correctedPriceId,
                productId,
                PriceType.SalePrice,
                110,
                start.AddMinutes(30),
                "TRY",
                start.AddHours(3)));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal("OVERLAPPING_EFFECTIVE_PRICE", (await ReadErrorAsync(overlap)).Error.Code);
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                "SELECT count(*) FROM catalog.product_prices WHERE product_price_id = @value;",
                correctedPriceId));

        await AssertCreatedAsync(
            client,
            "/api/v1/management/catalog/prices",
            new CreateProductPriceV1(
                correctedPriceId,
                productId,
                PriceType.SalePrice,
                110,
                start.AddHours(2),
                "TRY",
                start.AddHours(3)));
    }

    [Fact]
    public async Task ConcurrentDuplicateSkuRequestsHaveOneWinnerAndNoPartialRow()
    {
        using var firstClient = CreateClient(CatalogApiTestDatabase.ManagerToken);
        using var secondClient = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var sku = "RACE-" + Guid.NewGuid().ToString("N")[..8];
        var firstRequest = new CreateProductV1(
            Guid.NewGuid(), sku, "First", ProductType.MenuItem, StockMode.Untracked);
        var secondRequest = firstRequest with { Id = Guid.NewGuid(), Name = "Second" };

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/management/catalog/products", firstRequest),
            secondClient.PostAsJsonAsync("/api/v1/management/catalog/products", secondRequest));
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            var conflict = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("DUPLICATE_SKU", (await ReadErrorAsync(conflict)).Error.Code);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM catalog.products WHERE sku = @value;", sku));
    }

    [Fact]
    public async Task SettingAvailabilityChangesRowVersionAndReturnsTheProduct()
    {
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();
        var sku = "AVAIL-" + Guid.NewGuid().ToString("N")[..8];
        await AssertCreatedAsync(client, "/api/v1/management/catalog/products",
            new CreateProductV1(productId, sku, "Suspendable", ProductType.MenuItem, StockMode.Untracked));

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/management/catalog/products/{productId:D}/availability",
            new SetProductAvailabilityV1(false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProductV1>();
        Assert.NotNull(updated);
        Assert.False(updated.IsAvailable);
        // Regression coverage for an independent audit finding (2026-09-05,
        // B6): catalog.products had no row_version at all. The genuine
        // concurrency race (a second writer racing between this endpoint's
        // internal read and write) is exercised at the repository level in
        // PostgresRepositoryTests — an HTTP-level Task.WhenAll race is not
        // reliable here since both requests would need to land their reads
        // before either write, which two independent HttpClient calls
        // cannot guarantee.
        Assert.Equal(
            2L,
            await ScalarAsync<long>("SELECT row_version FROM catalog.products WHERE product_id = @value;", productId));
    }

    [Fact]
    public async Task CreatingAScheduledFuturePriceClearsAnAlreadyExpiredCurrentPrice()
    {
        // Found by an independent audit (2026-09-07): CreatePriceAsync only
        // ever set current_price when the JUST-INSERTED row itself was
        // already effective at that instant — inserting a future price left
        // whatever stale value was already cached untouched, even when that
        // cached value belonged to a price that had since expired with no
        // compensating write. Regression: an expired price is cached as
        // current_price (simulating a product nobody has touched since its
        // price window closed); inserting a scheduled future price for the
        // same product must now clear it to null, not leave the expired
        // amount silently charging.
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();
        var sku = "STALE-" + Guid.NewGuid().ToString("N")[..8];
        await AssertCreatedAsync(client, "/api/v1/management/catalog/products",
            new CreateProductV1(productId, sku, "Stale price product", ProductType.MenuItem, StockMode.Untracked));

        var expiredFrom = DateTimeOffset.UtcNow.AddHours(-2);
        var expiredTo = DateTimeOffset.UtcNow.AddHours(-1);
        await _database.ExecuteAsync(
            """
            INSERT INTO catalog.product_prices (product_price_id, product_id, price_type, price, currency_code, effective_from, effective_to)
            VALUES (@id, @product_id, 1, 100, 'TRY', @from, @to);
            """,
            ("id", Guid.NewGuid()), ("product_id", productId), ("from", expiredFrom), ("to", expiredTo));
        // Simulates the real bug's end state directly: this expired price's
        // amount is still cached, exactly as if nobody had called POST
        // /prices since it lapsed.
        await _database.ExecuteAsync(
            "UPDATE catalog.products SET current_price = 100 WHERE product_id = @product_id;",
            ("product_id", productId));

        await AssertCreatedAsync(client, "/api/v1/management/catalog/prices",
            new CreateProductPriceV1(
                Guid.NewGuid(), productId, PriceType.SalePrice, 150, DateTimeOffset.UtcNow.AddHours(1)));

        Assert.Null(await CurrentPriceAsync(productId));
    }

    [Fact]
    public async Task RecomputeAllAsyncActivatesScheduledPricesAndClearsExpiredOnesWithNoAccompanyingWrite()
    {
        // Regression coverage for the half CreatePriceAsync's own write-time
        // fix cannot reach: a scheduled/expiry boundary crossed with NOTHING
        // ever written again for that product. CatalogPriceRecomputeHostedService's
        // periodic sweep is the only thing that revisits current_price on
        // its own; this calls its static recompute pass directly rather than
        // waiting on the real interval.
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var activatingProductId = Guid.NewGuid();
        await AssertCreatedAsync(client, "/api/v1/management/catalog/products",
            new CreateProductV1(activatingProductId, $"ACT-{suffix}", "Now-active product", ProductType.MenuItem, StockMode.Untracked));
        // A price whose window already started, inserted directly (bypassing
        // the HTTP endpoint's own instant recompute) to simulate a price that
        // was scheduled well ahead of time and simply crossed into effect
        // with no one calling POST /prices at that moment.
        await _database.ExecuteAsync(
            """
            INSERT INTO catalog.product_prices (product_price_id, product_id, price_type, price, currency_code, effective_from, effective_to)
            VALUES (@id, @product_id, 1, 250, 'TRY', @from, NULL);
            """,
            ("id", Guid.NewGuid()), ("product_id", activatingProductId), ("from", DateTimeOffset.UtcNow.AddHours(-1)));
        Assert.Null(await CurrentPriceAsync(activatingProductId));

        var expiringProductId = Guid.NewGuid();
        await AssertCreatedAsync(client, "/api/v1/management/catalog/products",
            new CreateProductV1(expiringProductId, $"EXP-{suffix}", "Newly-expired product", ProductType.MenuItem, StockMode.Untracked));
        await _database.ExecuteAsync(
            """
            INSERT INTO catalog.product_prices (product_price_id, product_id, price_type, price, currency_code, effective_from, effective_to)
            VALUES (@id, @product_id, 1, 75, 'TRY', @from, @to);
            """,
            ("id", Guid.NewGuid()), ("product_id", expiringProductId),
            ("from", DateTimeOffset.UtcNow.AddHours(-2)), ("to", DateTimeOffset.UtcNow.AddMinutes(-1)));
        await _database.ExecuteAsync(
            "UPDATE catalog.products SET current_price = 75 WHERE product_id = @product_id;",
            ("product_id", expiringProductId));

        await CatalogPriceRecomputeHostedService.RecomputeAllAsync(_database.DataSource, CancellationToken.None);

        Assert.Equal(250m, await CurrentPriceAsync(activatingProductId));
        Assert.Null(await CurrentPriceAsync(expiringProductId));
    }

    [Fact]
    public async Task RecomputeAllAsyncNeverClearsASeedPriceForAProductWithNoPricingHistoryRow()
    {
        // Caught by a real regression while verifying this same wave: the
        // first version of RecomputeAllAsync's "expire" pass nulled out
        // EVERY product with no currently-effective product_prices row —
        // including a product that has NEVER had one at all, whose
        // current_price is a plain seed value set directly on the product
        // record (CreateProductV1.CurrentPrice, deep-catalog's own H-3
        // finding: creating a product this way never creates a
        // product_prices row). That is a legitimate, currently-supported
        // way to price a product; the sweep must only clear current_price
        // for a product that has actually entered the dated pricing system.
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();
        await AssertCreatedAsync(client, "/api/v1/management/catalog/products",
            new CreateProductV1(
                productId, "SEED-" + Guid.NewGuid().ToString("N")[..8], "Seed-priced product",
                ProductType.MenuItem, StockMode.Untracked, CurrentPrice: 42m));
        Assert.Equal(42m, await CurrentPriceAsync(productId));

        await CatalogPriceRecomputeHostedService.RecomputeAllAsync(_database.DataSource, CancellationToken.None);

        Assert.Equal(42m, await CurrentPriceAsync(productId));
    }

    [Fact]
    public async Task MissingForeignKeyIsStableValidationAndDoesNotInsertProduct()
    {
        using var client = CreateClient(CatalogApiTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/management/catalog/products",
            new CreateProductV1(
                productId,
                "FK-" + Guid.NewGuid().ToString("N")[..8],
                "Invalid reference",
                ProductType.MenuItem,
                StockMode.Untracked,
                Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("REFERENCE_NOT_FOUND", (await ReadErrorAsync(response)).Error.Code);
        Assert.Equal(
            0L,
            await ScalarAsync<long>("SELECT count(*) FROM catalog.products WHERE product_id = @value;", productId));
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
        {
            client.DefaultRequestHeaders.Add(
                "Cookie",
                $"{CatalogManagementEndpoints.ManagerCookieName}={token}");
        }
        return client;
    }

    private static async Task AssertCreatedAsync<T>(HttpClient client, string path, T request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<IReadOnlyList<Guid>> ReadIdsAsync<T>(
        HttpClient client,
        string resource,
        Func<T, Guid> id)
    {
        var page = await client.GetFromJsonAsync<CatalogPageV1<T>>(
            $"/api/v1/management/catalog/{resource}?limit=100");
        Assert.NotNull(page);
        return page.Items.Select(id).ToArray();
    }

    private static async Task<CatalogApiErrorEnvelopeV1> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<CatalogApiErrorEnvelopeV1>()
            ?? throw new InvalidOperationException("Expected a catalog error response.");

    private async Task<T> ScalarAsync<T>(string sql, object value)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("value", value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar result was null."));
    }

    private async Task<decimal?> CurrentPriceAsync(Guid productId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT current_price FROM catalog.products WHERE product_id = @product_id;");
        command.Parameters.AddWithValue("product_id", productId);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : (decimal)result;
    }
}
