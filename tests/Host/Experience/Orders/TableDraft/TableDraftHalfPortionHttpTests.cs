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

/// <summary>A table draft takes whole units, and half-unit steps only for a product that allows half portions.</summary>
[Collection("Order table-draft PostgreSQL HTTP")]
public sealed class TableDraftHalfPortionHttpTests : IAsyncLifetime
{
    private readonly OrderManagementTableDraftTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Theory]
    [InlineData(false, 1.5)]
    [InlineData(false, 0.5)]
    [InlineData(true, 1.3)]
    [InlineData(true, 0.25)]
    public async Task AQuantityTheProductDoesNotAllowIsRefusedAndNothingIsStored(bool allowsHalfPortion, double quantity)
    {
        var (productId, terminalId, cookie, tableId) = await SeedAsync(allowsHalfPortion);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, (decimal)quantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ORDER_QUANTITY_NOT_ALLOWED", body.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0L, await _database.ScalarAsync<long>("SELECT count(*) FROM orders.order_items;"));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(true, 1.5)]
    [InlineData(true, 0.5)]
    public async Task AnAllowedQuantityIsStoredExactly(bool allowsHalfPortion, double quantity)
    {
        var (productId, terminalId, cookie, tableId) = await SeedAsync(allowsHalfPortion);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, (decimal)quantity));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((decimal)quantity, await _database.ScalarAsync<decimal>("SELECT quantity FROM orders.order_items;"));
    }

    [Fact]
    public async Task ALineTheOrderAlreadyHoldsKeepsSavingAfterTheFlagIsSwitchedOffButANewFractionIsRefused()
    {
        var (productId, terminalId, cookie, tableId) = await SeedAsync(true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var lineId = Guid.NewGuid();
        using var first = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, 1.5m, lineId));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await _database.ExecuteAsync("UPDATE catalog.products SET allows_half_portion = false WHERE product_id = @id;", ("id", productId));

        using var same = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, 1.5m, lineId));
        using var another = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, 1.5m));

        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, another.StatusCode);
    }

    [Theory]
    [InlineData(false, 2.5)]
    [InlineData(true, 1.3)]
    public async Task AHeldLineWhoseQuantityChangesToADisallowedValueIsRefused(bool flagAfterFirstSave, double changedQuantity)
    {
        var (productId, terminalId, cookie, tableId) = await SeedAsync(true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var lineId = Guid.NewGuid();
        using var first = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, 1.5m, lineId));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await _database.ExecuteAsync(
            "UPDATE catalog.products SET allows_half_portion = @flag WHERE product_id = @id;",
            ("flag", flagAfterFirstSave), ("id", productId));

        using var changed = await client.SendAsync(JsonRequest(terminalId, cookie, tableId, productId, (decimal)changedQuantity, lineId));

        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal(1.5m, await _database.ScalarAsync<decimal>("SELECT quantity FROM orders.order_items;"));
    }

    private async Task<(Guid ProductId, Guid TerminalId, string Cookie, Guid TableId)> SeedAsync(bool allowsHalfPortion)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId);
        var tableId = await _database.SeedTableAsync();
        var productId = await _database.SeedProductAsync("Pilav", 40m);
        await _database.ExecuteAsync(
            "UPDATE catalog.products SET allows_half_portion = @flag WHERE product_id = @id;",
            ("flag", allowsHalfPortion), ("id", productId));
        return (productId, terminalId, cookie, tableId);
    }

    private static HttpRequestMessage JsonRequest(Guid terminalId, string cookie, Guid tableId, Guid productId, decimal quantity, Guid? lineId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/orders/table-draft")
        {
            Content = JsonContent.Create(new CreateTableDraftRequest(
                tableId, "M-01", [new OrderItemDraftDto(lineId ?? Guid.NewGuid(), productId, "Pilav", quantity, 40m)])),
        };
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
