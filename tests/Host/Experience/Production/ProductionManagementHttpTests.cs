using ALKAROS.Host.DualScreen;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.Production;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Production.Tests;

/// <summary>
/// V1-RMD-133: found by an independent audit (2026-09-09) — Production
/// (batch lifecycle + stock effects, both real and already tested at the
/// module level) had zero HTTP surface. These prove a real HTTP client can
/// run a batch through its full lifecycle and that completion really
/// consumes recipe ingredients and produces portion output.
/// </summary>
[Collection("Production management PostgreSQL HTTP")]
public sealed class ProductionManagementHttpTests : IAsyncLifetime
{
    private readonly ProductionManagementTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddProductionManagementExperience();

        _application = builder.Build();
        _application.MapProductionManagement();
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
        var request = new CreateProductionBatchV1("AUTH-" + Guid.NewGuid().ToString("N")[..8], Guid.NewGuid(), 10m);

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/management/production/batches", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(ProductionManagementTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync("/api/v1/management/production/batches", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);
    }

    [Fact]
    public async Task CompletingABatchOverHttpReallyConsumesIngredientsAndProducesOutput()
    {
        using var client = CreateClient(ProductionManagementTestDatabase.ManagerToken);
        var sourceLocationId = await _database.SeedStockLocationAsync("SRC-" + Guid.NewGuid().ToString("N")[..8]);
        var ingredientId = await _database.SeedStockItemAsync("ING-" + Guid.NewGuid().ToString("N")[..8]);
        var outputItemId = await _database.SeedStockItemAsync("OUT-" + Guid.NewGuid().ToString("N")[..8], "portion");
        // Recipe: 2kg of ingredient yields 10 portions -> 0.2kg per portion.
        var recipeVersionId = await _database.SeedRecipeVersionAsync(ingredientId, ingredientQuantityPerYield: 2m, yieldQuantity: 10m);
        await _database.SeedStockBalanceAsync(ingredientId, sourceLocationId, onHandQuantity: 100m);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/management/production/batches",
            new CreateProductionBatchV1("BATCH-" + Guid.NewGuid().ToString("N")[..8], recipeVersionId, 10m));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var batch = await createResponse.Content.ReadFromJsonAsync<ProductionBatchV1>();
        Assert.Equal("Planned", batch!.Status);

        // Completing before starting is rejected (the precondition
        // ExecuteBatchStockEffectsAsync itself doesn't enforce, restored at
        // the Host layer — see this endpoint's own doc comment).
        using var completeBeforeStart = await client.PostAsJsonAsync(
            $"/api/v1/management/production/batches/{batch.Id:D}/complete",
            new CompleteProductionBatchV1(10m, sourceLocationId, null, outputItemId));
        Assert.Equal(HttpStatusCode.Conflict, completeBeforeStart.StatusCode);
        Assert.Equal("INVALID_TRANSITION", (await ReadErrorAsync(completeBeforeStart)).Error.Code);

        using var start = await client.PostAsJsonAsync(
            $"/api/v1/management/production/batches/{batch.Id:D}/start", new StartProductionBatchV1(null));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal("InProgress", (await start.Content.ReadFromJsonAsync<ProductionBatchV1>())!.Status);

        using var complete = await client.PostAsJsonAsync(
            $"/api/v1/management/production/batches/{batch.Id:D}/complete",
            new CompleteProductionBatchV1(10m, sourceLocationId, null, outputItemId));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completeBody = await complete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", completeBody.GetProperty("batch").GetProperty("status").GetString());
        Assert.False(completeBody.GetProperty("wasAlreadyExecuted").GetBoolean());
        var consumption = Assert.Single(completeBody.GetProperty("consumptions").EnumerateArray());
        Assert.Equal(2m, consumption.GetProperty("quantity").GetDecimal());

        // Real inventory effect: 100 - 2 = 98 of the ingredient; 10 portions
        // of output at the source location (no separate destination given).
        Assert.Equal(98m, await _database.GetOnHandQuantityAsync(ingredientId, sourceLocationId));
        Assert.Equal(10m, await _database.GetOnHandQuantityAsync(outputItemId, sourceLocationId));

        using var getConsumptions = await client.GetAsync($"/api/v1/management/production/batches/{batch.Id:D}/consumptions");
        Assert.Equal(HttpStatusCode.OK, getConsumptions.StatusCode);
        var consumptions = await getConsumptions.Content.ReadFromJsonAsync<List<ProductionConsumptionV1>>();
        Assert.Single(consumptions!);
    }

    [Fact]
    public async Task InsufficientStockRejectsCompletionWithoutPartiallyApplyingEffects()
    {
        using var client = CreateClient(ProductionManagementTestDatabase.ManagerToken);
        var sourceLocationId = await _database.SeedStockLocationAsync("SRC-" + Guid.NewGuid().ToString("N")[..8]);
        var ingredientId = await _database.SeedStockItemAsync("ING-" + Guid.NewGuid().ToString("N")[..8]);
        var recipeVersionId = await _database.SeedRecipeVersionAsync(ingredientId, ingredientQuantityPerYield: 2m, yieldQuantity: 10m);
        await _database.SeedStockBalanceAsync(ingredientId, sourceLocationId, onHandQuantity: 1m); // Not enough for a full batch.

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/management/production/batches",
            new CreateProductionBatchV1("BATCH-" + Guid.NewGuid().ToString("N")[..8], recipeVersionId, 10m));
        var batch = await createResponse.Content.ReadFromJsonAsync<ProductionBatchV1>();
        await client.PostAsJsonAsync($"/api/v1/management/production/batches/{batch!.Id:D}/start", new StartProductionBatchV1(null));

        using var complete = await client.PostAsJsonAsync(
            $"/api/v1/management/production/batches/{batch.Id:D}/complete",
            new CompleteProductionBatchV1(10m, sourceLocationId, null, null));
        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
        Assert.Equal("INSUFFICIENT_STOCK", (await ReadErrorAsync(complete)).Error.Code);

        Assert.Equal(1m, await _database.GetOnHandQuantityAsync(ingredientId, sourceLocationId));
    }

    [Fact]
    public async Task CancelledBatchCannotBeStarted()
    {
        using var client = CreateClient(ProductionManagementTestDatabase.ManagerToken);
        var ingredientId = await _database.SeedStockItemAsync("ING-" + Guid.NewGuid().ToString("N")[..8]);
        var recipeVersionId = await _database.SeedRecipeVersionAsync(ingredientId, 1m, 10m);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/management/production/batches",
            new CreateProductionBatchV1("BATCH-" + Guid.NewGuid().ToString("N")[..8], recipeVersionId, 5m));
        var batch = await createResponse.Content.ReadFromJsonAsync<ProductionBatchV1>();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/v1/management/production/batches/{batch!.Id:D}/cancel", new CancelProductionBatchV1("Menu item pulled"));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<ProductionBatchV1>())!.Status);

        using var start = await client.PostAsJsonAsync(
            $"/api/v1/management/production/batches/{batch.Id:D}/start", new StartProductionBatchV1(null));
        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        Assert.Equal("INVALID_TRANSITION", (await ReadErrorAsync(start)).Error.Code);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ProductionManagementEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<ApiErrorEnvelope> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>()
            ?? throw new InvalidOperationException("Expected a production management error response.");
}

[CollectionDefinition("Production management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class ProductionManagementPostgresqlDefinition;
