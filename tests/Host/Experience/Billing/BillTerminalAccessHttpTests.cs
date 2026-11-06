using System.Net;
using System.Net.Http.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Billing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillTerminalAccessHttpTests : IAsyncLifetime
{
    private const string OtherTerminalCode = "BILL_OTHER_TERMINAL";
    private readonly BillingSplitTestDatabase _database = new();
    private NpgsqlDataSource _source = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _source = NpgsqlDataSource.Create(_database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        await _database.DisposeAsync();
    }

    public static TheoryData<string, string, string, object?> BillRoutes() => new()
    {
        { "POST", "cancel", "billing/bills/{b}/cancel", new { reason = "test" } },
        { "POST", "discount", "billing/bills/{b}/discount", new { idempotencyKey = "k", calculationType = "Percentage", value = 5, reasonCode = "Other" } },
        { "POST", "tip", "billing/bills/{b}/tip", new { idempotencyKey = "k", amount = 5 } },
        { "POST", "undo", "billing/bills/{b}/adjustments/{a}/undo", new { } },
        { "GET", "adjustments", "billing/bills/{b}/adjustments", null },
        { "POST", "refund-request", "billing/bills/{b}/refund-requests", new { paymentId = Guid.NewGuid() } },
        { "GET", "refund-list-for-bill", "billing/refund-requests?billId={b}", null },
        { "GET", "split-read", "billing/bills/{b}/split-design", null },
        { "GET", "split-owners", "billing/bills/{b}/split-design/owners", null },
        { "PUT", "split-equal", "billing/bills/{b}/split-design/equal", new { expectedBillRowVersion = 1, expectedAllocations = Array.Empty<object>(), owners = Array.Empty<object>() } },
        { "PUT", "split-items", "billing/bills/{b}/split-design/items", new { expectedBillRowVersion = 1, expectedAllocations = Array.Empty<object>(), targets = Array.Empty<object>() } },
        { "PUT", "split-amounts", "billing/bills/{b}/split-design/amounts", new { expectedBillRowVersion = 1, expectedAllocations = Array.Empty<object>(), targets = Array.Empty<object>() } },
        { "PUT", "split-custom", "billing/bills/{b}/split-design/custom", new { expectedBillRowVersion = 1, expectedAllocations = Array.Empty<object>(), targets = Array.Empty<object>() } },
        { "POST", "split-clear", "billing/bills/{b}/split-design/clear", new { expectedBillRowVersion = 1, expectedAllocations = Array.Empty<object>() } },
    };

    [Theory]
    [MemberData(nameof(BillRoutes))]
    public async Task AnotherTerminalIsRefusedAndTheOwnerAndAnOverrideHolderPass(string method, string name, string route, object? body)
    {
        var (billId, ownerTerminal, otherTerminal, ownerCookie, otherCookie, overrideCookie) = await SeedOwnedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var refused = await SendAsync(client, method, otherTerminal, route, billId, otherCookie, body);
        var owner = await SendAsync(client, method, ownerTerminal, route, billId, ownerCookie, body);
        var manager = await SendAsync(client, method, otherTerminal, route, billId, overrideCookie, body);

        Assert.True(HttpStatusCode.Forbidden == refused.Status && refused.Text.Contains(OtherTerminalCode), $"{name}: {refused.Status} {refused.Text}");
        Assert.True((int)owner.Status < 500 && !owner.Text.Contains(OtherTerminalCode), $"{name} owner: {owner.Status} {owner.Text}");
        Assert.True((int)manager.Status < 500 && !manager.Text.Contains(OtherTerminalCode), $"{name} manager: {manager.Status} {manager.Text}");
    }

    [Fact]
    public async Task OpeningTheBillOfAnOrderAnotherTerminalHoldsIsRefusedWithoutReturningTheBill()
    {
        var (billId, ownerTerminal, otherTerminal, ownerCookie, otherCookie, overrideCookie) = await SeedOwnedBillAsync();
        var orderId = await _database.OrderIdOfBillAsync(billId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var refused = await SendAsync(client, "POST", otherTerminal, "billing/bills/from-order/" + orderId, billId, otherCookie, null);
        var owner = await SendAsync(client, "POST", ownerTerminal, "billing/bills/from-order/" + orderId, billId, ownerCookie, null);
        var manager = await SendAsync(client, "POST", otherTerminal, "billing/bills/from-order/" + orderId, billId, overrideCookie, null);

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Contains(OtherTerminalCode, refused.Text);
        Assert.DoesNotContain(billId.ToString(), refused.Text);
        Assert.Equal(HttpStatusCode.OK, owner.Status);
        Assert.Equal(HttpStatusCode.OK, manager.Status);
    }

    [Fact]
    public async Task ANewBillBelongsToTheTerminalThatOpenedItAndAnotherTerminalCannotOpenItAgain()
    {
        var firstTerminal = Guid.NewGuid();
        var secondTerminal = Guid.NewGuid();
        var first = await _database.SeedSessionWithPermissionsAsync(firstTerminal, "cashier", "bills.split");
        var second = await _database.SeedSessionWithPermissionsAsync(secondTerminal, "cashier", "bills.split");
        var orderId = await _database.SeedOrderWithoutBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var opened = await SendAsync(client, "POST", firstTerminal, "billing/bills/from-order/" + orderId, Guid.Empty, first.Cookie, null);
        var refused = await SendAsync(client, "POST", secondTerminal, "billing/bills/from-order/" + orderId, Guid.Empty, second.Cookie, null);

        Assert.Equal(HttpStatusCode.OK, opened.Status);
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal(firstTerminal, await _source.CreateCommand("SELECT terminal_id FROM billing.bills WHERE order_id = '" + orderId + "';").ExecuteScalarAsync());
    }

    [Fact]
    public async Task ABillThatNobodyOwnsYetIsClaimedByTheFirstTerminalThatTouchesItAndKeptOnLaterTouches()
    {
        var seeded = await _database.SeedBillAsync();
        var repository = new PostgresBillTerminalOwnerRepository(_source);
        var firstTerminal = Guid.NewGuid();

        var claimed = await repository.ClaimAsync(seeded.BillId, firstTerminal);
        var later = await repository.ClaimAsync(seeded.BillId, Guid.NewGuid());

        Assert.Equal(firstTerminal, claimed);
        Assert.Equal(firstTerminal, later);
        Assert.Null(await repository.ClaimAsync(Guid.NewGuid(), firstTerminal));
    }

    [Fact]
    public async Task AUserWhoSeesAllBillsDoesNotClaimABillNobodyOwnsYet()
    {
        var seeded = await _database.SeedBillAsync();
        var terminal = Guid.NewGuid();
        var manager = await _database.SeedSessionWithPermissionsAsync(terminal, "manager", "bills.split", "cash.session.override");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await SendAsync(client, "GET", terminal, "billing/bills/{b}/split-design", seeded.BillId, manager.Cookie, null);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(DBNull.Value, await _source.CreateCommand("SELECT terminal_id FROM billing.bills WHERE bill_id = '" + seeded.BillId + "';").ExecuteScalarAsync());
    }

    [Fact]
    public async Task AReadDoesNotClaimABillNobodyOwnsYetButAWriteDoes()
    {
        var seeded = await _database.SeedBillAsync();
        var terminal = Guid.NewGuid();
        var cashier = await _database.SeedSessionWithPermissionsAsync(terminal, "cashier", "bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        Task<object?> Owner() => _source.CreateCommand("SELECT terminal_id FROM billing.bills WHERE bill_id = '" + seeded.BillId + "';").ExecuteScalarAsync();

        var read = await SendAsync(client, "GET", terminal, "billing/bills/{b}/split-design", seeded.BillId, cashier.Cookie, null);
        var ownerAfterRead = await Owner();
        await SendAsync(client, "POST", terminal, "billing/bills/{b}/split-design/clear", seeded.BillId, cashier.Cookie,
            new { expectedBillRowVersion = 1, expectedAllocations = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(DBNull.Value, ownerAfterRead);
        Assert.Equal(terminal, await Owner());
    }

    [Fact]
    public async Task CompletingARefundRequestThatDoesNotExistStillAnswersNotFound()
    {
        var terminal = Guid.NewGuid();
        var cashier = await _database.SeedSessionWithPermissionsAsync(terminal, "cashier", "payments.refund.request");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await SendAsync(client, "POST", terminal, $"billing/refund-requests/{Guid.NewGuid()}/complete", Guid.Empty, cashier.Cookie, new { slipNumber = (string?)null, note = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
    }

    [Fact]
    public async Task TerminalsClaimingTheSameBillAtOnceLeaveExactlyOneOwner()
    {
        var seeded = await _database.SeedBillAsync();
        var repository = new PostgresBillTerminalOwnerRepository(_source);
        var terminals = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();

        var owners = await Task.WhenAll(terminals.Select(terminal => repository.ClaimAsync(seeded.BillId, terminal)));

        Assert.Single(owners.Distinct());
        Assert.Contains(owners[0]!.Value, terminals);
    }

    [Fact]
    public async Task ACashierWithoutASessionOnTheTerminalStillGetsTheUnauthorizedAnswerNotARefusalOrAClaim()
    {
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await SendAsync(client, "GET", Guid.NewGuid(), "billing/bills/{b}/split-design", seeded.BillId, "x=y", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal(DBNull.Value, await _source.CreateCommand("SELECT terminal_id FROM billing.bills WHERE bill_id = '" + seeded.BillId + "';").ExecuteScalarAsync());
    }

    [Fact]
    public async Task CompletingARefundOfAnotherTerminalsBillIsRefusedAndTheListOfEveryBillsRefundsShowsOnlyTheBillsTheTerminalMayHandle()
    {
        var (billId, ownerTerminal, otherTerminal, ownerCookie, otherCookie, overrideCookie) = await SeedOwnedBillAsync();
        var ownerUser = (await _source.CreateCommand("SELECT user_id FROM identity.users LIMIT 1;").ExecuteScalarAsync())!;
        var paymentId = await _database.SeedPaymentAsync(billId, "Approved", 10m, (Guid)ownerUser);
        var requestId = Guid.NewGuid();
        await using (var insert = _source.CreateCommand(
            """
            INSERT INTO payments.refund_requests
                (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at, terminal_id)
            VALUES (@id, @bill, @payment, 5, 'Eft', 'test', 'AwaitingManualRefund', @user, now(), @terminal);
            """))
        {
            insert.Parameters.AddWithValue("id", requestId);
            insert.Parameters.AddWithValue("bill", billId);
            insert.Parameters.AddWithValue("payment", paymentId);
            insert.Parameters.AddWithValue("user", (Guid)ownerUser);
            insert.Parameters.AddWithValue("terminal", ownerTerminal);
            await insert.ExecuteNonQueryAsync();
        }
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var route = $"billing/refund-requests/{requestId}/complete";

        var refused = await SendAsync(client, "POST", otherTerminal, route, billId, otherCookie, new { slipNumber = (string?)null, note = "x" });
        var otherList = await SendAsync(client, "GET", otherTerminal, "billing/refund-requests", billId, otherCookie, null);
        var ownerList = await SendAsync(client, "GET", ownerTerminal, "billing/refund-requests", billId, ownerCookie, null);
        var managerList = await SendAsync(client, "GET", otherTerminal, "billing/refund-requests", billId, overrideCookie, null);

        Assert.True(HttpStatusCode.Forbidden == refused.Status && refused.Text.Contains(OtherTerminalCode), refused.Text);
        Assert.DoesNotContain(requestId.ToString(), otherList.Text);
        Assert.Contains(requestId.ToString(), ownerList.Text);
        Assert.Contains(requestId.ToString(), managerList.Text);
    }

    private async Task<(Guid BillId, Guid OwnerTerminal, Guid OtherTerminal, string OwnerCookie, string OtherCookie, string OverrideCookie)> SeedOwnedBillAsync()
    {
        var seeded = await _database.SeedBillAsync();
        var ownerTerminal = Guid.NewGuid();
        var otherTerminal = Guid.NewGuid();
        var owner = await _database.SeedSessionWithPermissionsAsync(ownerTerminal, "cashier",
            "bills.split", "bills.cancel", "payments.refund.request");
        var other = await _database.SeedSessionWithPermissionsAsync(otherTerminal, "cashier",
            "bills.split", "bills.cancel", "payments.refund.request");
        var manager = await _database.SeedSessionWithPermissionsAsync(otherTerminal, "manager",
            "bills.split", "bills.cancel", "payments.refund.request", "cash.session.override");
        await using var claim = _source.CreateCommand("UPDATE billing.bills SET terminal_id = @terminal WHERE bill_id = @bill;");
        claim.Parameters.AddWithValue("terminal", ownerTerminal);
        claim.Parameters.AddWithValue("bill", seeded.BillId);
        await claim.ExecuteNonQueryAsync();
        return (seeded.BillId, ownerTerminal, otherTerminal, owner.Cookie, other.Cookie, manager.Cookie);
    }

    private static async Task<(HttpStatusCode Status, string Text)> SendAsync(
        HttpClient client, string method, Guid terminalId, string route, Guid billId, string cookie, object? body)
    {
        var path = $"/api/v1/terminals/{terminalId:D}/" + route.Replace("{b}", billId.ToString("D")).Replace("{a}", Guid.NewGuid().ToString("D"));
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
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
