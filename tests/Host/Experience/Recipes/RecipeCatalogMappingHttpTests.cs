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
/// V11-RCP-003: which recipe a catalog product corresponds to — the first
/// step of the V1.1 actual-vs-theoretical variance report chain.
/// </summary>
[Collection("Recipe catalog mapping PostgreSQL HTTP")]
public sealed class RecipeCatalogMappingHttpTests : IAsyncLifetime
{
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
        var recipeId = await _database.SeedRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var productId = Guid.NewGuid();
        var request = new AssignProductRecipeMappingV1(recipeId, true, null);

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync($"/api/v1/management/recipes/products/{productId}/mapping", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var denied = CreateClient(RecipeCatalogMappingTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync($"/api/v1/management/recipes/products/{productId}/mapping", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var manager = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);
        using var confirmNotCreated = await manager.GetAsync($"/api/v1/management/recipes/products/{productId}/mapping");
        Assert.Equal(HttpStatusCode.NotFound, confirmNotCreated.StatusCode);
    }

    [Fact]
    public async Task AssigningAMappingOverHttpPersistsAndReadsBack()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);
        var recipeId = await _database.SeedRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var productId = Guid.NewGuid();

        using var assign = await client.PostAsJsonAsync(
            $"/api/v1/management/recipes/products/{productId}/mapping",
            new AssignProductRecipeMappingV1(recipeId, true, "porsiyon reçetesi"));
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var created = await assign.Content.ReadFromJsonAsync<ProductRecipeMappingV1>();
        Assert.Equal(recipeId, created!.RecipeId);
        Assert.True(created.IsActive);

        using var get = await client.GetAsync($"/api/v1/management/recipes/products/{productId}/mapping");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var loaded = await get.Content.ReadFromJsonAsync<ProductRecipeMappingV1>();
        Assert.Equal(recipeId, loaded!.RecipeId);
        Assert.Equal("porsiyon reçetesi", loaded.Notes);
    }

    [Fact]
    public async Task DeletingAMissingMappingReturnsNotFound()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();

        using var delete = await client.DeleteAsync($"/api/v1/management/recipes/products/{productId}/mapping");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task DeletingAnExistingMappingRemovesIt()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);
        var recipeId = await _database.SeedRecipeAsync("RCP-" + Guid.NewGuid().ToString("N")[..8]);
        var productId = Guid.NewGuid();

        await client.PostAsJsonAsync(
            $"/api/v1/management/recipes/products/{productId}/mapping",
            new AssignProductRecipeMappingV1(recipeId, true, null));

        using var delete = await client.DeleteAsync($"/api/v1/management/recipes/products/{productId}/mapping");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var get = await client.GetAsync($"/api/v1/management/recipes/products/{productId}/mapping");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task AssigningAMappingToANonExistentRecipeReturnsValidationError()
    {
        using var client = CreateClient(RecipeCatalogMappingTestDatabase.ManagerToken);
        var productId = Guid.NewGuid();

        using var assign = await client.PostAsJsonAsync(
            $"/api/v1/management/recipes/products/{productId}/mapping",
            new AssignProductRecipeMappingV1(Guid.NewGuid(), true, null));
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{RecipeCatalogMappingEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("Recipe catalog mapping PostgreSQL HTTP", DisableParallelization = true)]
public sealed class RecipeCatalogMappingPostgresqlDefinition;
