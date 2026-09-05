using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.TableDraft.Tests;

/// <summary>
/// Regression coverage for two Critical findings from an independent audit
/// (2026-09-06): (1) a second table-draft call for a table that already has
/// a Draft order used to replace its items instead of appending to them,
/// silently destroying whatever was already there; (2) neither production
/// client ever called submit-draft, so an order never actually left Draft
/// or reached the kitchen.
/// </summary>
[Collection("Order table-draft PostgreSQL HTTP")]
public sealed class OrderManagementTableDraftHttpTests : IAsyncLifetime
{
    private readonly OrderManagementTableDraftTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            DraftPath(Guid.NewGuid()),
            new CreateTableDraftRequest(Guid.NewGuid(), "M-01", "Garson", []));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASecondDraftForTheSameTableAppendsToTheExistingOrderInsteadOfReplacingIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedProductAsync("Çorba", 60m);
        var dessert = await _database.SeedProductAsync("Baklava", 90m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05", "Garson Ahmet",
                [new OrderItemDraftDto(starter, "Çorba", 1, 60m)])));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstDraft = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var secondResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-05", "Garson Ahmet",
                [new OrderItemDraftDto(dessert, "Baklava", 1, 90m)])));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondDraft = await secondResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(firstDraft!.OrderId, secondDraft!.OrderId);
        Assert.Equal(2, secondDraft.Items.Count);
        Assert.Contains(secondDraft.Items, item => item.ProductName == "Çorba");
        Assert.Contains(secondDraft.Items, item => item.ProductName == "Baklava");
        Assert.Equal(150m, secondDraft.TotalAmount);
    }

    [Fact]
    public async Task SubmitDraftMovesTheOrderToSubmittedStatus()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var draftResponse = await client.SendAsync(JsonRequest(
            DraftPath(terminalId), cookie,
            new CreateTableDraftRequest(tableId, "M-07", "Garson Ahmet",
                [new OrderItemDraftDto(product, "Köfte", 2, 280m)])));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var submitResponse = await client.SendAsync(JsonRequest(
            SubmitPath(terminalId, draft!.OrderId), cookie,
            new SubmitTableOrderRequest(draft.OrderId, draft.RowVersion)));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Submitted", submitted!.Status);
    }

    private static string DraftPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/orders/table-draft";

    private static string SubmitPath(Guid terminalId, Guid orderId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/submit-draft";

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddOrderManagementExperience();
        var app = builder.Build();
        app.MapOrderManagementApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}

[CollectionDefinition("Order table-draft PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderTableDraftPostgresqlDefinition;
