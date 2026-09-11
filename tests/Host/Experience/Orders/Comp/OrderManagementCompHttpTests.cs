using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Orders.Comp.Tests;

/// <summary>
/// V1-BIL-005: `ItemExceptionHandler.ApplyComplimentaryAsync` (V1-ORD-003)
/// already worked; nothing called it, and `IAuthorizationGrantService
/// .RequestAsync` (V1-IAM-019/020/021/023) had zero HTTP callers anywhere in
/// the codebase. This is the first real caller — proves the direct-grant
/// fast path, the escalate-to-pending path, DelegationEscalationResolver
/// actually resolving a grant, BehaviouralTighteningGate actually forcing
/// escalation, and the approve-then-retry-same-idempotency-key path.
/// </summary>
[Collection("Order comp PostgreSQL HTTP")]
public sealed class OrderManagementCompHttpTests : IAsyncLifetime
{
    private readonly OrderManagementCompTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            CompPath(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnInvalidReasonCodeIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "NotARealReason"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ARoleThatHoldsBillsCompOutrightAppliesDirectly()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "VIPGuest"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Applied", body!.Status);
        Assert.Equal("Complimentary", body.NewItemStatus);
        Assert.Equal(2, body.NewOrderRowVersion);
    }

    [Fact]
    public async Task ARoleWithoutBillsCompAndNoPolicyOrDelegationEscalatesToPending()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Pending", body!.Status);
        Assert.NotNull(body.GrantId);
    }

    [Fact]
    public async Task AnActiveDelegationResolvesTheGrantAndAppliesDirectly()
    {
        var terminalId = Guid.NewGuid();
        var (waiterUserId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var (managerUserId, _) = await _database.SeedCashierSessionAsync(Guid.NewGuid(), "manager", "bills.comp");
        await _database.SeedActiveDelegationAsync(waiterUserId, managerUserId, "bills.comp", limitAmount: 500m);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "ServiceApology"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Applied", body!.Status);
    }

    [Fact]
    public async Task AnOpenBehaviouralTighteningForcesEscalationEvenWithAnAlwaysAllowPolicy()
    {
        var terminalId = Guid.NewGuid();
        var (waiterUserId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        await _database.SeedPolicyAsync("bills.comp", "waiter", "always_allow");
        await _database.SeedOpenBehaviouralTighteningAsync(waiterUserId, "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var response = await client.SendAsync(request);

        // Without the open tightening, always_allow would auto-approve this
        // (see the sibling policy-only assertion below is not needed — the
        // point of this test is that the tightening overrides it).
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Pending", body!.Status);
    }

    [Fact]
    public async Task ApprovingAPendingGrantThenRetryingTheSameIdempotencyKeyCompletesIt()
    {
        var terminalId = Guid.NewGuid();
        var (waiterUserId, cookie) = await _database.SeedCashierSessionAsync(terminalId, "waiter");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var idempotencyKey = Guid.NewGuid().ToString();
        var body = new ApplyComplimentaryRequestV1(idempotencyKey, 1, "CustomerSatisfaction");

        using var first = await client.SendAsync(JsonRequest(CompPath(terminalId, orderId, itemId), cookie, body));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var pending = await first.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();

        await _database.ApproveGrantAsync(pending!.GrantId!.Value, Guid.NewGuid());

        using var second = await client.SendAsync(JsonRequest(CompPath(terminalId, orderId, itemId), cookie, body));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var applied = await second.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Applied", applied!.Status);
    }

    [Fact]
    public async Task AWaiterCompingAnotherServersCheckIsRefusedByTheOwnCheckGuard()
    {
        // V1-RMD-111: proves the wiring end to end, not just the guard's own
        // unit tests (AuthorizationGrantServiceTests already cover the guard
        // logic in isolation) — before this task, every HTTP caller passed
        // SubjectServingUserId: null, so this 403 never actually happened.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        var otherServerId = Guid.NewGuid();
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(otherServerId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AWaiterCompingTheirOwnCheckIsNotBlockedByTheOwnCheckGuard()
    {
        var terminalId = Guid.NewGuid();
        var (waiterUserId, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(waiterUserId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var response = await client.SendAsync(request);

        // The guard only refuses a mismatch; on a match, the request falls
        // through to the normal policy path, same as before ServingUserId
        // existed (no policy seeded here -> escalates to pending).
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Pending", body!.Status);
    }

    // V1-WTR-012 (garson audit follow-on ideation, 2026-09-11): a small,
    // self-service daily comp allowance for the waiter role — ₺50/kalem,
    // ₺150/gün. The shared SeedActiveOrderWithOneItemAsync() default (₺100,
    // 10% tax) is deliberately ABOVE the per-item cap, so every test above
    // that uses it exercises the unaffected escalate-to-pending path exactly
    // as before; these tests seed a cheap item on purpose to reach the new
    // resolver.

    [Fact]
    public async Task AWaiterWithinTheDailyPersonalBudgetAppliesDirectlyWithoutAManager()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(
            waiterId, unitPrice: 30m, taxRate: 0m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Applied", body!.Status);
        // ₺150 daily cap - this ₺30 comp = ₺120 left for the rest of today.
        Assert.Equal(120m, body.PersonalBudgetRemaining);
    }

    [Fact]
    public async Task ARoleThatHoldsBillsCompOutrightNeverReportsAPersonalBudgetRemainder()
    {
        // The existing direct-grant test (above) doesn't assert this field;
        // pinning it separately so a future change to the endpoint can't
        // quietly start reporting a personal-budget number for a role that
        // was never on that path at all.
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId, "supervisor", "bills.comp");
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(
            servingUserId: null, unitPrice: 30m, taxRate: 0m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "VIPGuest"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Null(body!.PersonalBudgetRemaining);
    }

    [Fact]
    public async Task AWaiterOverThePerItemCapStillEscalatesToPendingInsteadOfUsingTheBudget()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        // ₺60 - one ₺10 over the ₺50 per-item cap; still comfortably under
        // the ₺150 daily cap, so this isolates the per-item check.
        var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(
            waiterId, unitPrice: 60m, taxRate: 0m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Pending", body!.Status);
    }

    [Fact]
    public async Task AWaiterAtTheDailyCapEscalatesTheNextCompToPendingInsteadOfOverspendingTheBudget()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, cookie) = await _database.SeedRealWaiterSessionAsync(terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // Three ₺50 comps exhaust the ₺150 daily cap exactly.
        for (var i = 0; i < 3; i++)
        {
            var (orderId, itemId) = await _database.SeedActiveOrderWithOneItemAsync(
                waiterId, unitPrice: 50m, taxRate: 0m);
            using var spend = JsonRequest(CompPath(terminalId, orderId, itemId), cookie,
                new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
            using var spendResponse = await client.SendAsync(spend);
            Assert.Equal(HttpStatusCode.OK, spendResponse.StatusCode);
        }

        // A fourth ₺50 comp would push the day to ₺200 - over the ₺150 cap -
        // so it must fall back to a manager, not silently apply.
        var (fourthOrderId, fourthItemId) = await _database.SeedActiveOrderWithOneItemAsync(
            waiterId, unitPrice: 50m, taxRate: 0m);
        using var fourth = JsonRequest(CompPath(terminalId, fourthOrderId, fourthItemId), cookie,
            new ApplyComplimentaryRequestV1(Guid.NewGuid().ToString(), 1, "CustomerSatisfaction"));
        using var fourthResponse = await client.SendAsync(fourth);

        Assert.Equal(HttpStatusCode.Accepted, fourthResponse.StatusCode);
        var body = await fourthResponse.Content.ReadFromJsonAsync<ApplyComplimentaryResultV1>();
        Assert.Equal("Pending", body!.Status);
    }

    private static string CompPath(Guid terminalId, Guid orderId, Guid itemId)
        => $"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items/{itemId:D}/comp";

    private static HttpRequestMessage JsonRequest(string path, string cookie, ApplyComplimentaryRequestV1 body)
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

[CollectionDefinition("Order comp PostgreSQL HTTP", DisableParallelization = true)]
public sealed class OrderCompPostgresqlDefinition;
