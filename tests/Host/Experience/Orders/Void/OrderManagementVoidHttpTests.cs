using System.Net;
using System.Net.Http.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.Void.Tests;

/// <summary>
/// V1-ORD-005: `ItemExceptionHandler.VoidItemAsync` (V1-ORD-003) already
/// worked; nothing called it. This exercises the HTTP contract wrapped
/// around it — session + orders.create enforcement, the not-yet-sent
/// guard, and the response/error envelope.
/// </summary>
[Collection("Order void PostgreSQL HTTP")]
public sealed class OrderManagementVoidHttpTests : IAsyncLifetime
{
    private readonly OrderManagementVoidTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            VoidPath(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new VoidOrderItemRequestV1(1, "CustomerChange"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ANotYetSentItemIsVoided()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithOrdersCreateAsync(terminalId);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(VoidPath(terminalId, orderId, itemId), cookie,
            new VoidOrderItemRequestV1(1, "CustomerChange", "müşteri vazgeçti"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VoidOrderItemResultV1>();
        Assert.Equal("Cancelled", body!.NewItemStatus);
        Assert.Equal(2, body.NewOrderRowVersion);

        // V1-RMD-244: IAuditEventStore existed since V1-OPS-001 with zero
        // real callers on this endpoint.
        var auditEvents = new PostgresAuditEventStore(_database.DataSource);
        var events = await auditEvents.GetByAggregateAsync("OrderItem", itemId);
        var applied = Assert.Single(events);
        Assert.Equal("order-item.voided", applied.EventName);
        Assert.Equal("CustomerChange", applied.Reason);
    }

    [Fact]
    public async Task WithoutOrdersCreateIsForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithoutOrdersCreateAsync(terminalId);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(VoidPath(terminalId, orderId, itemId), cookie,
            new VoidOrderItemRequestV1(1, "CustomerChange"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnInvalidReasonCodeIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithOrdersCreateAsync(terminalId);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(VoidPath(terminalId, orderId, itemId), cookie,
            new VoidOrderItemRequestV1(1, "NotARealReason"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnItemAlreadySentToTheKitchenCannotBeVoidedHere()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithOrdersCreateAsync(terminalId);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(sent: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(VoidPath(terminalId, orderId, itemId), cookie,
            new VoidOrderItemRequestV1(1, "CustomerChange"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static string VoidPath(Guid terminalId, Guid orderId, Guid itemId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items/{itemId:D}/void";

    private static HttpRequestMessage JsonRequest(string path, string cookie, VoidOrderItemRequestV1 body)
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

[CollectionDefinition("Order void PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderVoidPostgresqlDefinition;
