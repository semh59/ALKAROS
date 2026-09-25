using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Recipes.Tests;

/// <summary>
/// V1-RMD-275: nothing in the running host could create a recipe, so the product
/// mapping and cost snapshot surfaces had nothing to point at. Proves a manager can
/// create a recipe, build a draft version, activate it, that an activated version is
/// immutable, and that custom unit conversions can be added and listed.
/// </summary>
[Collection("Recipe catalog mapping PostgreSQL HTTP")]
public sealed class RecipeManagementHttpTests : IAsyncLifetime
{
    private const string Base = "/api/v1/management/recipes";

    private readonly RecipeCatalogMappingTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddRecipeCatalogMappingExperience();

        _application = builder.Build();
        _application.MapRecipeCatalogMappingApi();
        await _application.StartAsync();
        var addresses = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task RecipeManagementNeedsAManagerSession()
    {
        using var anonymous = CreateClient(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Base)).StatusCode);
        using var denied = CreateClient(RecipeCatalogMappingTestDatabase.DeniedToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync(Base, new CreateRecipeV1("RCP-X", "Yok"))).StatusCode);
    }

    [Fact]
    public async Task AManagerBuildsActivatesAndThenCannotChangeAVersionedRecipe()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);
        var stockItem = await _database.SeedStockItemAsync("ING-" + Guid.NewGuid().ToString("N")[..8]);
        var code = "RCP-" + Guid.NewGuid().ToString("N")[..8];

        var created = await client.PostAsJsonAsync(Base, new CreateRecipeV1(code, "Mercimek çorbası", "Günlük"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var recipe = await created.Content.ReadFromJsonAsync<RecipeV1>();

        var duplicate = await client.PostAsJsonAsync(Base, new CreateRecipeV1(code, "Aynı kod"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var draftResponse = await client.PostAsJsonAsync($"{Base}/{recipe!.Id:D}/versions", new CreateRecipeDraftV1(10m, "kg", 30, "Kaynat"));
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<RecipeVersionV1>();
        Assert.Equal("Draft", draft!.Status);
        Assert.Equal(1, draft.VersionNumber);

        var withIngredient = await client.PostAsJsonAsync(
            $"{Base}/versions/{draft.Id:D}/ingredients", new AddRecipeIngredientV1(stockItem, 500m, "g", 5m, 1));
        Assert.Equal(HttpStatusCode.OK, withIngredient.StatusCode);
        Assert.Single((await withIngredient.Content.ReadFromJsonAsync<RecipeVersionV1>())!.Ingredients);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Base}/{recipe.Id:D}/versions/1/activate", null)).StatusCode);
        var listed = await client.GetFromJsonAsync<RecipeVersionV1[]>($"{Base}/{recipe.Id:D}/versions");
        Assert.Equal("Active", listed!.Single().Status);

        // Activated versions are immutable: cost snapshots and consumption records rely on it.
        var refused = await client.PostAsJsonAsync(
            $"{Base}/versions/{draft.Id:D}/ingredients", new AddRecipeIngredientV1(stockItem, 100m, "g"));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VERSION_IMMUTABLE", error.GetProperty("error").GetProperty("code").GetString());

        // A change goes through a new draft version.
        var next = await client.PostAsync($"{Base}/{recipe.Id:D}/versions/next", null);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Equal(2, (await next.Content.ReadFromJsonAsync<RecipeVersionV1>())!.VersionNumber);
        Assert.Equal(2, (await client.GetFromJsonAsync<RecipeVersionV1[]>($"{Base}/{recipe.Id:D}/versions"))!.Length);
    }

    [Fact]
    public async Task UnknownRecipesAndBadDraftInputAreRefused()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/{Guid.NewGuid():D}/versions")).StatusCode);

        var recipe = await (await client.PostAsJsonAsync(Base, new CreateRecipeV1("RCP-" + Guid.NewGuid().ToString("N")[..8], "Test")))
            .Content.ReadFromJsonAsync<RecipeV1>();
        var badYield = await client.PostAsJsonAsync($"{Base}/{recipe!.Id:D}/versions", new CreateRecipeDraftV1(0m, "kg"));
        Assert.Equal(HttpStatusCode.BadRequest, badYield.StatusCode);
    }

    [Fact]
    public async Task CustomUnitConversionsCanBeAddedListedAndReplacedPerPair()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);

        var created = await client.PostAsJsonAsync($"{Base}/unit-conversions", new CreateUnitConversionV1("kasa", "adet", 12m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var listed = await client.GetFromJsonAsync<UnitConversionV1[]>($"{Base}/unit-conversions");
        Assert.Contains(listed!, item => item.FromUnitCode == "kasa" && item.ToUnitCode == "adet" && item.Factor == 12m);

        // The pair is unique: posting it again replaces the factor, it never adds a second row.
        var again = await client.PostAsJsonAsync($"{Base}/unit-conversions", new CreateUnitConversionV1("kasa", "adet", 24m));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var afterwards = await client.GetFromJsonAsync<UnitConversionV1[]>($"{Base}/unit-conversions");
        Assert.Equal(24m, Assert.Single(afterwards!, item => item.FromUnitCode == "kasa" && item.ToUnitCode == "adet").Factor);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{RecipeCatalogMappingEndpoints.ManagerCookieName}={token}");
        return client;
    }
}
