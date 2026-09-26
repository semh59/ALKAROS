using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Recipes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Recipes.Tests;

/// <summary>
/// V1-RMD-247: found by an independent audit (2026-09-18) —
/// IRecipeCostSnapshotService (V11-RCP-002) had zero HTTP surface. Proves a
/// real HTTP client can create a real cost snapshot (waste factor + moving
/// average cost via a fallback cost), and that CostPerPortion (the audit's
/// own second finding — CalculatedCost was never divided by YieldQuantity
/// anywhere) is now computed and returned correctly.
/// </summary>
[Collection("Recipe cost snapshot PostgreSQL HTTP")]
public sealed class RecipeCostSnapshotHttpTests : IAsyncLifetime
{
    private readonly RecipeCostSnapshotTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddRecipeCostSnapshotExperience();

        _application = builder.Build();
        _application.MapRecipeCostSnapshotApi();
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
        var itemId = await _database.SeedStockItemAsync("RMD247-AUTH");
        var versionId = await _database.SeedRecipeVersionAsync(itemId, ingredientQuantityPerYield: 1m);
        var body = new CreateRecipeCostSnapshotV1(
            new DateOnly(2026, 1, 1),
            StockItemUnits: new Dictionary<Guid, string> { [itemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [itemId] = 10m });

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync($"/api/v1/management/recipes/{versionId:D}/cost-snapshots/", body);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var denied = CreateClient(RecipeCostSnapshotTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync($"/api/v1/management/recipes/{versionId:D}/cost-snapshots/", body);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AManagerCanCreateASnapshotAndItsCostPerPortionIsCorrect()
    {
        // 2 kg flour per batch, 10% waste, yielding 10 portions, fallback
        // cost 20 TRY/kg -> effective 2.2 kg * 20 = 44 TRY total, 4.40 TRY/portion.
        var itemId = await _database.SeedStockItemAsync("RMD247-FLOUR");
        var versionId = await _database.SeedRecipeVersionAsync(
            itemId, ingredientQuantityPerYield: 2m, lossPercentage: 10m, yieldQuantity: 10m);
        using var client = CreateClient(RecipeCostSnapshotTestDatabase.ManagerToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/management/recipes/{versionId:D}/cost-snapshots/",
            new CreateRecipeCostSnapshotV1(
                new DateOnly(2026, 1, 1),
                StockItemUnits: new Dictionary<Guid, string> { [itemId] = "kg" },
                FallbackItemCosts: new Dictionary<Guid, decimal> { [itemId] = 20m }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotV1>();
        Assert.Equal(44.00m, snapshot!.CalculatedCost);
        Assert.Equal(4.40m, snapshot.CostPerPortion);
        Assert.Single(snapshot.Items);

        using var effective = await client.GetAsync(
            $"/api/v1/management/recipes/{versionId:D}/cost-snapshots/effective?asOfDate=2026-06-01");
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        var effectiveSnapshot = await effective.Content.ReadFromJsonAsync<RecipeCostSnapshotV1>();
        Assert.Equal(snapshot.Id, effectiveSnapshot!.Id);
    }

    [Fact]
    public async Task CreatingTheSameSnapshotTwiceIsRejected()
    {
        var itemId = await _database.SeedStockItemAsync("RMD247-DUP");
        var versionId = await _database.SeedRecipeVersionAsync(itemId, ingredientQuantityPerYield: 1m);
        using var client = CreateClient(RecipeCostSnapshotTestDatabase.ManagerToken);
        var body = new CreateRecipeCostSnapshotV1(
            new DateOnly(2026, 1, 1),
            StockItemUnits: new Dictionary<Guid, string> { [itemId] = "kg" },
            FallbackItemCosts: new Dictionary<Guid, decimal> { [itemId] = 5m });

        using var first = await client.PostAsJsonAsync($"/api/v1/management/recipes/{versionId:D}/cost-snapshots/", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync($"/api/v1/management/recipes/{versionId:D}/cost-snapshots/", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AnIngredientWithNoCostBasisAtAllIsRejected()
    {
        var itemId = await _database.SeedStockItemAsync("RMD247-NOCOST");
        var versionId = await _database.SeedRecipeVersionAsync(itemId, ingredientQuantityPerYield: 1m);
        using var client = CreateClient(RecipeCostSnapshotTestDatabase.ManagerToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/management/recipes/{versionId:D}/cost-snapshots/",
            new CreateRecipeCostSnapshotV1(
                new DateOnly(2026, 1, 1),
                StockItemUnits: new Dictionary<Guid, string> { [itemId] = "kg" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // V1-RMD-333 (independent 2026-09-26 audit, orta seviye bulgu): before this, omitting
    // StockItemUnits entirely silently defaulted to the ingredient's own native unit - here
    // that happens to equal the seeded stock item's real tracking unit ("kg"), which is exactly
    // why the gap was invisible to every earlier test of this endpoint. Now it fails loud with
    // its own distinct error code instead of ever risking a silently wrong cost.
    [Fact]
    public async Task OmittingTheStockUnitMappingIsRejectedWithItsOwnErrorCode()
    {
        var itemId = await _database.SeedStockItemAsync("RMD333-NOUNIT");
        var versionId = await _database.SeedRecipeVersionAsync(itemId, ingredientQuantityPerYield: 1m);
        using var client = CreateClient(RecipeCostSnapshotTestDatabase.ManagerToken);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/management/recipes/{versionId:D}/cost-snapshots/",
            new CreateRecipeCostSnapshotV1(
                new DateOnly(2026, 1, 1), FallbackItemCosts: new Dictionary<Guid, decimal> { [itemId] = 5m }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("MISSING_STOCK_UNIT_MAPPING", text);
    }

    [Fact]
    public async Task NoEffectiveSnapshotIsNotFound()
    {
        var itemId = await _database.SeedStockItemAsync("RMD247-NONE");
        var versionId = await _database.SeedRecipeVersionAsync(itemId, ingredientQuantityPerYield: 1m);
        using var client = CreateClient(RecipeCostSnapshotTestDatabase.ManagerToken);

        using var response = await client.GetAsync(
            $"/api/v1/management/recipes/{versionId:D}/cost-snapshots/effective?asOfDate=2026-01-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{RecipeCostSnapshotEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("Recipe cost snapshot PostgreSQL HTTP", DisableParallelization = true)]
public sealed class RecipeCostSnapshotPostgresqlDefinition;
