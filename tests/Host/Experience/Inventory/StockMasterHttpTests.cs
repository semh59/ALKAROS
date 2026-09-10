using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.Inventory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Inventory.Tests;

/// <summary>
/// V1-RMD-143: <c>StockMasterEndpoints</c> itself (stock locations/items and
/// product-stock mappings) had zero dedicated HTTP coverage — its behavior
/// was only ever exercised indirectly through Orders/NFC's own stock
/// consumption assertions. These prove a real HTTP client can configure a
/// product's BOM and see the computed available quantity, and that the
/// manager-only permission gate is actually enforced.
/// </summary>
[Collection("Inventory management PostgreSQL HTTP")]
public sealed class StockMasterHttpTests : IAsyncLifetime
{
    private readonly StockMasterTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddStockMasterExperience();

        _application = builder.Build();
        _application.MapStockMasterApi();
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
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task MissingAndUnpermissionedSessionsAreRejectedWithoutMutation()
    {
        var request = new CreateStockLocationV1("AUTH-" + Guid.NewGuid().ToString("N")[..8], "Auth Test", "Warehouse");

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/management/inventory/stock-locations", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(StockMasterTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync("/api/v1/management/inventory/stock-locations", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);
    }

    [Fact]
    public async Task CreatingAStockLocationOverHttpPersistsIt()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var code = "LOC-" + Guid.NewGuid().ToString("N")[..8];

        using var create = await client.PostAsJsonAsync(
            "/api/v1/management/inventory/stock-locations", new CreateStockLocationV1(code, "Main Kitchen", "Kitchen"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<StockLocationV1>();
        // StockLocation normalizes its code to upper-invariant on construction.
        Assert.Equal(code.ToUpperInvariant(), created!.Code);
        Assert.Equal("Kitchen", created.LocationType);
        Assert.True(created.IsActive);

        using var list = await client.GetAsync("/api/v1/management/inventory/stock-locations?activeOnly=true");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var locations = await list.Content.ReadFromJsonAsync<List<StockLocationV1>>();
        Assert.Contains(locations!, l => l.Id == created.Id);
    }

    [Fact]
    public async Task CreatingAStockItemOverHttpPersistsIt()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var locationId = await _database.SeedStockLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        var code = "ITEM-" + Guid.NewGuid().ToString("N")[..8];

        using var create = await client.PostAsJsonAsync(
            "/api/v1/management/inventory/stock-items",
            new CreateStockItemV1(code, "Chicken Breast", "RawMaterial", "kg", locationId));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<StockItemV1>();
        // StockItem normalizes its code to upper-invariant on construction.
        Assert.Equal(code.ToUpperInvariant(), created!.Code);
        Assert.Equal(locationId, created.DefaultLocationId);

        using var list = await client.GetAsync("/api/v1/management/inventory/stock-items?activeOnly=true");
        var items = await list.Content.ReadFromJsonAsync<List<StockItemV1>>();
        Assert.Contains(items!, i => i.Id == created.Id);
    }

    [Fact]
    public async Task DuplicateStockItemCodeIsRejectedWithConflict()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var code = "DUP-" + Guid.NewGuid().ToString("N")[..8];
        var request = new CreateStockItemV1(code, "Duplicate", "RawMaterial", "kg");

        using var first = await client.PostAsJsonAsync("/api/v1/management/inventory/stock-items", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync("/api/v1/management/inventory/stock-items", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("DUPLICATE_RESOURCE", (await ReadErrorAsync(second)).Error.Code);
    }

    [Fact]
    public async Task AssigningAndListingAProductStockMappingComputesAvailableQuantity()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var locationId = await _database.SeedStockLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8], locationId, "adet");
        await _database.SeedStockBalanceAsync(stockItemId, locationId, onHandQuantity: 40m);
        // V1-RMD-156: product_stock_mappings.product_id is FK'd to
        // catalog.products now — a mapping pointing at a product that never
        // existed (a typo, or one since deleted) used to insert cleanly and
        // then silently never match at Accept time, so stock quietly stopped
        // being consumed for that product with no error anywhere. A bare Guid
        // standing in for "a product not created yet" was the same failure
        // mode by another name; the product has to exist first.
        var productId = await _database.SeedProductAsync("SKU-" + Guid.NewGuid().ToString("N")[..8]);

        using var assign = await client.PostAsJsonAsync(
            $"/api/v1/management/inventory/products/{productId:D}/stock-mappings",
            new AssignProductStockMappingV1(stockItemId, QuantityMultiplier: 2m));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        Assert.Equal(1, await _database.CountProductStockMappingsAsync(productId));

        using var list = await client.GetAsync($"/api/v1/management/inventory/products/{productId:D}/stock-mappings");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var mappings = await list.Content.ReadFromJsonAsync<List<ProductStockMappingV1>>();
        var mapping = Assert.Single(mappings!);
        Assert.Equal(stockItemId, mapping.StockItemId);
        Assert.Equal(2m, mapping.QuantityMultiplier);
        // 40 on hand / multiplier 2 -> 20 sellable units of this product.
        Assert.Equal(20m, mapping.AvailableQuantity);
    }

    [Fact]
    public async Task AModifierStockMappingCanBeAssignedListedAndRemoved()
    {
        // V1-RMD-152: a modifier is not a catalog.products row, so the
        // product route above cannot express what an extra consumes. Same
        // three operations, its own table.
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var locationId = await _database.SeedStockLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8], locationId, "adet");
        await _database.SeedStockBalanceAsync(stockItemId, locationId, onHandQuantity: 30m);
        // V1-RMD-156: modifier_stock_mappings.modifier_id is FK'd to
        // catalog.modifiers now, same reasoning as the product mapping above.
        var modifierId = await _database.SeedModifierAsync("MOD-" + Guid.NewGuid().ToString("N")[..8]);

        using var assign = await client.PostAsJsonAsync(
            $"/api/v1/management/inventory/modifiers/{modifierId:D}/stock-mappings",
            new AssignModifierStockMappingV1(stockItemId, QuantityMultiplier: 3m));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        using var list = await client.GetAsync($"/api/v1/management/inventory/modifiers/{modifierId:D}/stock-mappings");
        var mappings = await list.Content.ReadFromJsonAsync<List<ModifierStockMappingV1>>();
        var mapping = Assert.Single(mappings!);
        Assert.Equal(stockItemId, mapping.StockItemId);
        Assert.Equal(3m, mapping.QuantityMultiplier);
        // 30 on hand / multiplier 3 -> 10 helpings of this extra.
        Assert.Equal(10m, mapping.AvailableQuantity);

        using var remove = await client.DeleteAsync(
            $"/api/v1/management/inventory/modifiers/{modifierId:D}/stock-mappings/{stockItemId:D}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        using var afterRemoval = await client.GetAsync($"/api/v1/management/inventory/modifiers/{modifierId:D}/stock-mappings");
        Assert.Empty((await afterRemoval.Content.ReadFromJsonAsync<List<ModifierStockMappingV1>>())!);
    }

    [Fact]
    public async Task RemovingAModifierMappingThatDoesNotExistIsNotFound()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);

        using var remove = await client.DeleteAsync(
            $"/api/v1/management/inventory/modifiers/{Guid.NewGuid():D}/stock-mappings/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [Fact]
    public async Task UnmappedProductHasAnEmptyMappingList()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        using var list = await client.GetAsync($"/api/v1/management/inventory/products/{Guid.NewGuid():D}/stock-mappings");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var mappings = await list.Content.ReadFromJsonAsync<List<ProductStockMappingV1>>();
        Assert.Empty(mappings!);
    }

    [Fact]
    public async Task MappingAgainstAnUnknownStockItemIsRejectedWithoutMutation()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();

        using var assign = await client.PostAsJsonAsync(
            $"/api/v1/management/inventory/products/{productId:D}/stock-mappings",
            new AssignProductStockMappingV1(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, assign.StatusCode);
        Assert.Equal("NOT_FOUND", (await ReadErrorAsync(assign)).Error.Code);
        Assert.Equal(0, await _database.CountProductStockMappingsAsync(productId));
    }

    /// <summary>
    /// V1-RMD-143 follow-up (2026-09-09 deep review): a manager mapping a
    /// product to the wrong stock item had no way to undo it —
    /// IProductStockMappingRepository.RemoveAsync existed but nothing ever
    /// called it.
    /// </summary>
    [Fact]
    public async Task RemovingAProductStockMappingDeletesItAndOnlyIt()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);
        var stockItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8]);
        var otherStockItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8]);
        // V1-RMD-156: needs a real product now — see the note on
        // AssigningAndListingAProductStockMappingComputesAvailableQuantity.
        var productId = await _database.SeedProductAsync("SKU-" + Guid.NewGuid().ToString("N")[..8]);
        using var first = await client.PostAsJsonAsync(
            $"/api/v1/management/inventory/products/{productId:D}/stock-mappings",
            new AssignProductStockMappingV1(stockItemId));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await client.PostAsJsonAsync(
            $"/api/v1/management/inventory/products/{productId:D}/stock-mappings",
            new AssignProductStockMappingV1(otherStockItemId));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, await _database.CountProductStockMappingsAsync(productId));

        using var delete = await client.DeleteAsync(
            $"/api/v1/management/inventory/products/{productId:D}/stock-mappings/{stockItemId:D}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Only the targeted (product, stockItem) pair is gone — the other
        // mapping for the same product survives untouched.
        Assert.Equal(1, await _database.CountProductStockMappingsAsync(productId));
        using var list = await client.GetAsync($"/api/v1/management/inventory/products/{productId:D}/stock-mappings");
        var remaining = Assert.Single((await list.Content.ReadFromJsonAsync<List<ProductStockMappingV1>>())!);
        Assert.Equal(otherStockItemId, remaining.StockItemId);
    }

    [Fact]
    public async Task RemovingANonExistentMappingIsRefusedWithNotFound()
    {
        using var client = CreateClient(StockMasterTestDatabase.ManagerToken);

        using var delete = await client.DeleteAsync(
            $"/api/v1/management/inventory/products/{Guid.NewGuid():D}/stock-mappings/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal("NOT_FOUND", (await ReadErrorAsync(delete)).Error.Code);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{StockMasterEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<StockMasterApiErrorEnvelopeV1> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<StockMasterApiErrorEnvelopeV1>()
            ?? throw new InvalidOperationException("Expected a stock master error response.");
}

[CollectionDefinition("Inventory management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class StockMasterPostgresqlDefinition;
