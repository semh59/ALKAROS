using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Purchasing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Purchasing.Tests;

/// <summary>
/// V1-RMD-132: found by an independent audit (2026-09-09) — Purchasing
/// (suppliers, purchase orders, goods receipt with variance policy, all
/// real and already tested at the module level) had zero HTTP surface.
/// These are the first end-to-end tests proving a real HTTP client can
/// reach it, including that a goods receipt really moves inventory.
/// </summary>
[Collection("Purchasing management PostgreSQL HTTP")]
public sealed class PurchasingManagementHttpTests : IAsyncLifetime
{
    private readonly PurchasingManagementTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddPurchasingManagementExperience();

        _application = builder.Build();
        _application.MapPurchasingManagement();
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
        var request = new CreateSupplierV1("AUTH-" + Guid.NewGuid().ToString("N")[..8], "Protected Supplier", null, null, null, null);

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/management/purchasing/suppliers", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(PurchasingManagementTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync("/api/v1/management/purchasing/suppliers", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);
    }

    [Fact]
    public async Task ManagerCanRunTheFullPurchaseToReceiptFlowAndItReallyMovesInventory()
    {
        using var client = CreateClient(PurchasingManagementTestDatabase.ManagerToken);
        var locationId = await _database.SeedStockLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8]);

        using var createSupplier = await client.PostAsJsonAsync(
            "/api/v1/management/purchasing/suppliers",
            new CreateSupplierV1("SUP-" + Guid.NewGuid().ToString("N")[..8], "Acme Foods", "1234567890", null, null, "acme@example.com"));
        Assert.Equal(HttpStatusCode.Created, createSupplier.StatusCode);
        var supplier = await createSupplier.Content.ReadFromJsonAsync<SupplierViewV1>();
        Assert.False(supplier!.IsMasked); // The creating manager always sees its own write unmasked.

        using var getSupplier = await client.GetAsync($"/api/v1/management/purchasing/suppliers/{supplier.Id:D}");
        Assert.Equal(HttpStatusCode.OK, getSupplier.StatusCode);

        using var createOrder = await client.PostAsJsonAsync(
            "/api/v1/management/purchasing/purchase-orders",
            new CreatePurchaseOrderV1(
                "PO-" + Guid.NewGuid().ToString("N")[..8], supplier.Id, locationId,
                [new CreatePurchaseOrderLineV1(stockItemId, 10m, "kg", 25m)], null));
        Assert.Equal(HttpStatusCode.Created, createOrder.StatusCode);
        var order = await createOrder.Content.ReadFromJsonAsync<PurchaseOrderV1>();
        Assert.Equal("Draft", order!.Status);
        Assert.Equal(250m, order.TotalAmount);
        var line = Assert.Single(order.Lines);

        using var submit = await client.PostAsync($"/api/v1/management/purchasing/purchase-orders/{order.Id:D}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal("Submitted", (await submit.Content.ReadFromJsonAsync<PurchaseOrderV1>())!.Status);

        Assert.Equal(0m, await _database.GetOnHandQuantityAsync(stockItemId, locationId));

        using var receive = await client.PostAsJsonAsync(
            $"/api/v1/management/purchasing/purchase-orders/{order.Id:D}/receipts",
            new ReceiveGoodsV1("GRN-" + Guid.NewGuid().ToString("N")[..8], [new ReceiveGoodsLineV1(line.Id, 10m, null)]));
        Assert.Equal(HttpStatusCode.Created, receive.StatusCode);
        var receipt = await receive.Content.ReadFromJsonAsync<GoodsReceiptV1>();
        Assert.Equal("Purchasing Manager", receipt!.ReceivedBy);
        Assert.Equal(10m, Assert.Single(receipt.Items).AcceptedQuantity);

        // The whole point of goods receipt: real inventory moved.
        Assert.Equal(10m, await _database.GetOnHandQuantityAsync(stockItemId, locationId));

        using var getOrderAfterReceipt = await client.GetAsync($"/api/v1/management/purchasing/purchase-orders/{order.Id:D}");
        Assert.Equal("Completed", (await getOrderAfterReceipt.Content.ReadFromJsonAsync<PurchaseOrderV1>())!.Status);

        // The order is now fully received (Completed) — a second receipt
        // against it, duplicate receipt number or not, is rejected by the
        // order's own status guard before the receipt-number uniqueness
        // check is ever reached.
        using var receiveAgain = await client.PostAsJsonAsync(
            $"/api/v1/management/purchasing/purchase-orders/{order.Id:D}/receipts",
            new ReceiveGoodsV1(receipt.ReceiptNumber, [new ReceiveGoodsLineV1(line.Id, 10m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, receiveAgain.StatusCode);
        Assert.Equal("INVALID_STATUS", (await ReadErrorAsync(receiveAgain)).Error.Code);
    }

    [Fact]
    public async Task DeactivatingASupplierBlocksNewPurchaseOrders()
    {
        using var client = CreateClient(PurchasingManagementTestDatabase.ManagerToken);
        var locationId = await _database.SeedStockLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        var stockItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8]);

        using var createSupplier = await client.PostAsJsonAsync(
            "/api/v1/management/purchasing/suppliers",
            new CreateSupplierV1("SUP-" + Guid.NewGuid().ToString("N")[..8], "Inactive Co", null, null, null, null));
        var supplier = await createSupplier.Content.ReadFromJsonAsync<SupplierViewV1>();

        using var deactivate = await client.PostAsync($"/api/v1/management/purchasing/suppliers/{supplier!.Id:D}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        using var createOrder = await client.PostAsJsonAsync(
            "/api/v1/management/purchasing/purchase-orders",
            new CreatePurchaseOrderV1(
                "PO-" + Guid.NewGuid().ToString("N")[..8], supplier.Id, locationId,
                [new CreatePurchaseOrderLineV1(stockItemId, 1m, "kg", 1m)], null));
        Assert.Equal(HttpStatusCode.Conflict, createOrder.StatusCode);
        Assert.Equal("SUPPLIER_INACTIVE", (await ReadErrorAsync(createOrder)).Error.Code);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{PurchasingManagementEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<PurchasingApiErrorEnvelopeV1> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<PurchasingApiErrorEnvelopeV1>()
            ?? throw new InvalidOperationException("Expected a purchasing management error response.");
}

[CollectionDefinition("Purchasing management PostgreSQL HTTP", DisableParallelization = true)]
public sealed class PurchasingManagementPostgresqlDefinition;
