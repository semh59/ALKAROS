using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

/// <summary>A terminal pays, prints and charges only its own bills; a holder of cash.session.override does so on any (real host, real Postgres).</summary>
[Collection("PaymentTender HTTP")]
public sealed class BillTerminalAccessTenderHttpTests : IAsyncLifetime
{
    private const string OtherTerminalCode = "BILL_OTHER_TERMINAL";
    private readonly PaymentTenderHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd844-webroot-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    public static TheoryData<string, string, string, object?> BillRoutes() => new()
    {
        { "GET", "tenders-read", "billing/bills/{b}/tenders/", null },
        { "POST", "tender", "billing/bills/{b}/tenders/", new { method = "Eft", amount = 1, idempotencyKey = "k-tender" } },
        { "POST", "not-charged", "billing/bills/{b}/tenders/unsettled/{x}/not-charged", new { reason = "test" } },
        { "POST", "card-charged", "billing/bills/{b}/tenders/unsettled/{x}/card-charged", new { slipNumber = "SLIP-1234", note = "x" } },
        { "POST", "confirmation-approve", "billing/bills/{b}/tenders/confirmations/{x}/approve", new { note = "x" } },
        { "POST", "confirmation-reject", "billing/bills/{b}/tenders/confirmations/{x}/reject", new { note = "x" } },
        { "POST", "show-on-display", "billing/bills/{b}/show-on-display", null },
        { "GET", "receipt", "billing/bills/{b}/receipt", null },
        { "POST", "receipt-print", "billing/bills/{b}/receipt/print", null },
        { "POST", "account-charge", "billing/bills/{b}/account-charge", new { customerId = Guid.NewGuid(), amount = 1, idempotencyKey = "k-account" } },
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
    public async Task ACashTenderForAnotherTerminalsBillIsRefusedWhileTheOwnerAndAnOverrideHolderPass()
    {
        var (billId, ownerTerminal, otherTerminal, ownerCookie, otherCookie, overrideCookie) = await SeedOwnedBillAsync("cash.drawer");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var ownerSession = await OpenCashSessionAsync(client, ownerTerminal, ownerCookie);
        var otherSession = await OpenCashSessionAsync(client, otherTerminal, otherCookie);
        object Body(string key) => new { billId, amountDue = 1, tenderedAmount = 1, idempotencyKey = key };

        var refused = await SendAsync(client, "POST", otherTerminal, $"cash-sessions/{otherSession}/cash-tender", billId, otherCookie, Body("k-refused"));
        var owner = await SendAsync(client, "POST", ownerTerminal, $"cash-sessions/{ownerSession}/cash-tender", billId, ownerCookie, Body("k-owner"));
        var manager = await SendAsync(client, "POST", otherTerminal, $"cash-sessions/{otherSession}/cash-tender", billId, overrideCookie, Body("k-manager"));

        Assert.True(HttpStatusCode.Forbidden == refused.Status && refused.Text.Contains(OtherTerminalCode), $"{refused.Status} {refused.Text}");
        Assert.True((int)owner.Status < 500 && !owner.Text.Contains(OtherTerminalCode), $"owner: {owner.Status} {owner.Text}");
        Assert.True((int)manager.Status < 500 && !manager.Text.Contains(OtherTerminalCode), $"manager: {manager.Status} {manager.Text}");
    }

    [Fact]
    public async Task ARejectedWriteLeavesTheBillUnownedAndAnAcceptedOneClaimsIt()
    {
        var billId = await _database.SeedBillAsync(100m);
        var terminal = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminal, "claim-" + Guid.NewGuid().ToString("N"), true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var rejected = await SendAsync(client, "POST", terminal, "billing/bills/{b}/tenders/", billId, cookie, new { method = "Eft", amount = 0, idempotencyKey = "k-bad" });
        var ownerAfterRejection = await OwnerAsync(billId);
        var accepted = await SendAsync(client, "POST", terminal, "billing/bills/{b}/tenders/", billId, cookie, new { method = "Eft", amount = 1, idempotencyKey = "k-ok" });

        Assert.True((int)rejected.Status is >= 400 and < 500, $"{rejected.Status} {rejected.Text}");
        Assert.Null(ownerAfterRejection);
        Assert.True((int)accepted.Status < 300, $"{accepted.Status} {accepted.Text}");
        Assert.Equal(terminal, await OwnerAsync(billId));
    }

    private async Task<Guid?> OwnerAsync(Guid billId)
    {
        await using var command = _database.DataSource.CreateCommand("SELECT terminal_id FROM billing.bills WHERE bill_id = @bill;");
        command.Parameters.AddWithValue("bill", billId);
        return await command.ExecuteScalarAsync() as Guid?;
    }

    private static async Task<Guid> OpenCashSessionAsync(HttpClient client, Guid terminalId, string cookie)
    {
        var opened = await SendAsync(client, "POST", terminalId, "cash-sessions", Guid.Empty, cookie, new { openingBalance = 0 });
        Assert.True(opened.Status is HttpStatusCode.Created or HttpStatusCode.OK, $"{opened.Status} {opened.Text}");
        using var document = System.Text.Json.JsonDocument.Parse(opened.Text);
        return document.RootElement.GetProperty("cashSessionId").GetGuid();
    }

    private async Task<(Guid BillId, Guid OwnerTerminal, Guid OtherTerminal, string OwnerCookie, string OtherCookie, string OverrideCookie)> SeedOwnedBillAsync(params string[] extraPermissions)
    {
        var billId = await _database.SeedBillAsync(100m);
        var ownerTerminal = Guid.NewGuid();
        var otherTerminal = Guid.NewGuid();
        var ownerCookie = await _database.SeedCashierSessionAsync(ownerTerminal, "owner-" + Guid.NewGuid().ToString("N"), true, extraPermissions);
        var otherCookie = await _database.SeedCashierSessionAsync(otherTerminal, "other-" + Guid.NewGuid().ToString("N"), true, extraPermissions);
        var overrideCookie = await _database.SeedCashierSessionAsync(otherTerminal, "manager-" + Guid.NewGuid().ToString("N"), true, [.. extraPermissions, "cash.session.override"]);
        await using var claim = _database.DataSource.CreateCommand("UPDATE billing.bills SET terminal_id = @terminal WHERE bill_id = @bill;");
        claim.Parameters.AddWithValue("terminal", ownerTerminal);
        claim.Parameters.AddWithValue("bill", billId);
        await claim.ExecuteNonQueryAsync();
        return (billId, ownerTerminal, otherTerminal, ownerCookie, otherCookie, overrideCookie);
    }

    private static async Task<(HttpStatusCode Status, string Text)> SendAsync(
        HttpClient client, string method, Guid terminalId, string route, Guid billId, string cookie, object? body)
    {
        var path = $"/api/v1/terminals/{terminalId:D}/" + route.Replace("{b}", billId.ToString("D")).Replace("{x}", Guid.NewGuid().ToString("D"));
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString,
            _webRoot,
            "http://127.0.0.1:0",
            TrustedProxies: [IPAddress.Loopback]));
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
