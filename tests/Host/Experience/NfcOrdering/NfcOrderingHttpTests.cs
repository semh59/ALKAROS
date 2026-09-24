using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.NfcOrdering.Tests;

/// <summary>
/// V12-NFC-001: an NFC tap has no session of any kind — every request here
/// is anonymous, unlike the cashier/waiter-authenticated table-draft tests
/// it otherwise mirrors (V1-RMD-123's idempotency pattern).
/// </summary>
[Collection("NFC ordering PostgreSQL HTTP")]
public sealed class NfcOrderingHttpTests : IAsyncLifetime
{
    private readonly NfcOrderingTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task TheCatalogEndpointListsAvailableProductsWithoutAnySession()
    {
        var tableId = await _database.SeedTableAsync();
        await _database.SeedProductAsync("Çorba", 60m);
        await _database.SeedProductAsync("Köfte", 280m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync($"/api/v1/nfc/tables/{tableId:D}/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<CatalogProductDto>>();
        Assert.Equal(2, products!.Count);
        Assert.Contains(products, p => p.Name == "Çorba" && p.UnitPrice == 60m);
        Assert.Contains(products, p => p.Name == "Köfte" && p.UnitPrice == 280m);
    }

    [Fact]
    public async Task AnAvailableTableSelfChecksInAndTheOrderIsAcceptedWithAKitchenTicket()
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 2)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Accepted", order!.Status);
        Assert.Equal(120m, order.TotalAmount);

        var (status, currentOrderId, _) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Occupied", status);
        Assert.Equal(order.OrderId, currentOrderId);
        Assert.Equal(1, await _database.KitchenTicketCountAsync(order.OrderId));
    }

    [Theory]
    [InlineData(200, HttpStatusCode.OK)]
    [InlineData(201, HttpStatusCode.BadRequest)]
    public async Task ALineNoteIsCappedAtTwoHundredCharactersOnTheServer(int length, HttpStatusCode expected)
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1, new string('a', length))], Guid.NewGuid()));

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.Contains("İstek doğrulanamadı.", text);
            Assert.DoesNotContain("cannot exceed", text);
        }
    }

    [Fact]
    public async Task RetryingTheSameSubmissionReplaysTheExistingOrderInsteadOfDuplicatingIt()
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Köfte", 280m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var payload = new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1)], Guid.NewGuid());

        using var first = await client.PostAsJsonAsync(OrdersPath(tableId), payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstOrder = await first.Content.ReadFromJsonAsync<OrderDto>();

        using var retry = await client.PostAsJsonAsync(OrdersPath(tableId), payload);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryOrder = await retry.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(firstOrder!.OrderId, retryOrder!.OrderId);
        Assert.Equal(1, await _database.KitchenTicketCountAsync(firstOrder.OrderId));
    }

    [Fact]
    public async Task ConcurrentIdenticalFirstSubmissionsResolveToTheSameOrder()
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Ayran", 20m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var payload = new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1)], Guid.NewGuid());

        var first = client.PostAsJsonAsync(OrdersPath(tableId), payload);
        var second = client.PostAsJsonAsync(OrdersPath(tableId), payload);
        var responses = await Task.WhenAll(first, second);

        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var firstOrder = await responses[0].Content.ReadFromJsonAsync<OrderDto>();
            var secondOrder = await responses[1].Content.ReadFromJsonAsync<OrderDto>();
            Assert.Equal(firstOrder!.OrderId, secondOrder!.OrderId);
            Assert.Equal(1, await _database.KitchenTicketCountAsync(firstOrder.OrderId));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task ASecondVisitToAnOccupiedTableCreatesASeparateOrderWithoutChangingTableState()
    {
        var tableId = await _database.SeedTableAsync();
        var starter = await _database.SeedProductAsync("Çorba", 60m);
        var dessert = await _database.SeedProductAsync("Baklava", 90m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var firstResponse = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), starter, 1)], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstOrder = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();

        using var secondResponse = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), dessert, 1)], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondOrder = await secondResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.NotEqual(firstOrder!.OrderId, secondOrder!.OrderId);
        Assert.Equal("Accepted", secondOrder.Status);

        var (status, _, rowVersionAfterSecond) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Occupied", status);
        // Only the first (self-check-in) visit touches the table row; the
        // second visit's row_version must not have advanced past that.
        Assert.Equal(2, rowVersionAfterSecond);
        Assert.Equal(2, await _database.KitchenTicketCountAsync(firstOrder.OrderId) + await _database.KitchenTicketCountAsync(secondOrder.OrderId));
    }

    [Fact]
    public async Task AnAgeRestrictedItemWithholdsTheTrustedShortcutAndReservesTheTable()
    {
        var tableId = await _database.SeedTableAsync();
        var beer = await _database.SeedProductAsync("Bira", 120m, isAgeRestricted: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), beer, 1)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("PendingConfirmation", order!.Status);

        var (status, currentOrderId, _) = await _database.GetTableStateAsync(tableId);
        Assert.Equal("Reserved", status);
        Assert.Equal(order.OrderId, currentOrderId);
    }

    [Fact]
    public async Task AMixedCartWithOneAgeRestrictedItemStillWithholdsTheWholeOrder()
    {
        var tableId = await _database.SeedTableAsync();
        var soup = await _database.SeedProductAsync("Çorba", 60m);
        var wine = await _database.SeedProductAsync("Şarap", 350m, isAgeRestricted: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest(
                [new NfcOrderItemRequestDto(Guid.NewGuid(), soup, 1), new NfcOrderItemRequestDto(Guid.NewGuid(), wine, 1)],
                Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("PendingConfirmation", order!.Status);
        Assert.Equal(410m, order.TotalAmount);
    }

    [Fact]
    public async Task RetryingAnAgeRestrictedSubmissionReplaysTheSamePendingOrder()
    {
        var tableId = await _database.SeedTableAsync();
        var beer = await _database.SeedProductAsync("Bira", 120m, isAgeRestricted: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var payload = new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), beer, 1)], Guid.NewGuid());

        using var first = await client.PostAsJsonAsync(OrdersPath(tableId), payload);
        var firstOrder = await first.Content.ReadFromJsonAsync<OrderDto>();

        using var retry = await client.PostAsJsonAsync(OrdersPath(tableId), payload);
        var retryOrder = await retry.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(firstOrder!.OrderId, retryOrder!.OrderId);
        Assert.Equal("PendingConfirmation", retryOrder.Status);
        Assert.Equal(1, await _database.KitchenTicketCountAsync(firstOrder.OrderId));
    }

    [Fact]
    public async Task AReservedTableRefusesNfcSelfService()
    {
        var tableId = await _database.SeedTableAsync(status: "Reserved");
        var product = await _database.SeedProductAsync("Kola", 45m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownTableIsNotFound()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(Guid.NewGuid()),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), 1)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmptyItemsIsRejectedWithATurkishMessage()
    {
        // V1-RMD-136: found by an independent audit (2026-09-09) — this and
        // the two validation branches below ran ahead of
        // NfcOrderingExceptionFilter and returned raw English literals
        // straight to the customer's phone (NfcOrder.tsx renders the
        // message verbatim) — the same class of leak V1-RMD-127 fixed the
        // same day in OrderManagementEndpoints.cs, just missed here.
        var tableId = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sipariş kalemleri boş olamaz.", body);
        Assert.DoesNotContain("cannot be empty", body);
    }

    [Fact]
    public async Task AnEmptyTableIdIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(Guid.Empty),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), 1)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Masa kimliği boş olamaz.", body);
        Assert.DoesNotContain("cannot be empty", body);
    }

    [Fact]
    public async Task AnEmptySubmissionIdIsRejectedWithATurkishMessage()
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1)], Guid.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Gönderim kimliği boş olamaz.", body);
        Assert.DoesNotContain("cannot be empty", body);
    }

    [Fact]
    public async Task AnUnknownProductIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), Guid.NewGuid(), 1)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUnavailableProductIsRejected()
    {
        // V1-RMD-128: found by an independent audit (2026-09-09) — this
        // endpoint checked catalog.products.active but not is_available
        // (the real-time 86/suspend toggle a manager flips through
        // CatalogManagementStore.SetProductAvailabilityV1). No staff
        // member is in the loop on this self-service path, so a suspended
        // item could still be ordered straight through to the kitchen.
        var tableId = await _database.SeedTableAsync();
        var suspended = await _database.SeedProductAsync("Tükendi", 60m, isAvailable: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), suspended, 1)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// V1-RMD-138: found by an independent audit (2026-09-09) — this
    /// anonymous, unauthenticated endpoint had no upper bound on a line's
    /// quantity; with no staff member in the loop, a malicious or buggy
    /// client could submit an absurd quantity straight through to the
    /// kitchen.
    /// </summary>
    [Fact]
    public async Task AQuantityOverTheLimitIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId),
            new NfcOrderRequest([new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1000)], Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>V1-RMD-138: same finding as AQuantityOverTheLimitIsRejected, for the item-line-count cap instead of per-line quantity.</summary>
    [Fact]
    public async Task TooManyItemLinesIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var product = await _database.SeedProductAsync("Çorba", 60m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var items = Enumerable.Range(0, 51)
            .Select(_ => new NfcOrderItemRequestDto(Guid.NewGuid(), product, 1))
            .ToArray();

        using var response = await client.PostAsJsonAsync(
            OrdersPath(tableId), new NfcOrderRequest(items, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string OrdersPath(Guid tableId) => $"/api/v1/nfc/tables/{tableId:D}/orders";

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        // MapNfcOrderingApi requires the "nfc-order" named policy to exist;
        // the real Host (DualScreenApplication) registers it with a real
        // window, this standalone test host just needs it to be present.
        builder.Services.AddRateLimiter(options =>
            options.AddPolicy("nfc-order", _ =>
                System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test")));
        builder.Services.AddNfcOrderingExperience();
        var app = builder.Build();
        app.UseRateLimiter();
        app.MapNfcOrderingApi();
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

[CollectionDefinition("NFC ordering PostgreSQL HTTP", DisableParallelization = true)]
public sealed class NfcOrderingPostgresqlDefinition;
