using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Web;
using ALKAROS.Host.Experience.QrOrdering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.RelaySettings.Tests;

/// <summary>
/// A manager issues, rotates and revokes each table's printed QR; the address it returns must be one the customer
/// side of the same host accepts.
/// </summary>
[Collection("Relay settings PostgreSQL HTTP")]
public sealed class TableQrManagementHttpTests : IAsyncLifetime
{
    private const string Hostname = "sube1.example.app";

    private readonly RelaySettingsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task WithoutAManagerCookieEveryRouteIsUnauthorized()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var tableId = await _database.SeedTableAsync("1");
        var cashierCookie = await _database.SeedCashierSessionWithPermissionsAsync(Guid.NewGuid(), "manager", "integrations.manage");

        foreach (var request in new[]
        {
            Request(HttpMethod.Get, ListPath, cashierCookie),
            Request(HttpMethod.Post, ActionPath(tableId, "issue"), cashierCookie),
            Request(HttpMethod.Post, ActionPath(tableId, "rotate"), cashierCookie),
            Request(HttpMethod.Post, ActionPath(tableId, "revoke"), cashierCookie),
            Request(HttpMethod.Get, ListPath, cookie: null),
        })
        {
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ASupervisorSessionIsRefusedEvenWithTheGrant()
    {
        var cookie = await _database.SeedManagementSessionAsync("supervisor", Guid.NewGuid(), "supervisor", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Request(HttpMethod.Get, ListPath, cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AManagementSessionWithoutIntegrationsManageIsForbiddenOnEveryRoute()
    {
        var cookie = await _database.SeedManagementSessionAsync("manager", Guid.NewGuid(), "cashier", "reports.view");
        var tableId = await _database.SeedTableAsync("1");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        foreach (var request in new[]
        {
            Request(HttpMethod.Get, ListPath, cookie),
            Request(HttpMethod.Post, ActionPath(tableId, "issue"), cookie),
            Request(HttpMethod.Post, ActionPath(tableId, "rotate"), cookie),
            Request(HttpMethod.Post, ActionPath(tableId, "revoke"), cookie),
        })
        {
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("FORBIDDEN", await ErrorCodeAsync(response));
        }

        Assert.Equal(0, await _database.ActiveTokenCountAsync(tableId));
    }

    [Fact]
    public async Task ATimedOutTokenShowsAsNoQrAndIssueReplacesItOverHttp()
    {
        var cookie = await ManagerCookieAsync();
        var tableId = await _database.SeedTableAsync("1");
        await _database.SeedTunnelAsync(Hostname);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var stale = await IssueAsync(client, cookie, tableId);
        await _database.ExecuteAsync(
            "UPDATE qr_ordering.table_tokens SET expires_at = issued_at + interval '1 millisecond' WHERE table_id = @table_id;",
            ("table_id", tableId));

        using (var list = await client.SendAsync(Request(HttpMethod.Get, ListPath, cookie)))
        {
            var rows = (await list.Content.ReadFromJsonAsync<List<TableQrRowV1>>(JsonOptions))!;
            Assert.Null(Assert.Single(rows, row => row.TableId == tableId).QrIssuedAt);
        }

        var fresh = await IssueAsync(client, cookie, tableId);

        using var oldSession = await PostSessionAsync(client, RawToken(stale));
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        using var newSession = await PostSessionAsync(client, RawToken(fresh));
        Assert.Equal(HttpStatusCode.OK, newSession.StatusCode);
    }

    [Fact]
    public async Task TheListShowsEachSeatWithItsZoneAndQrStateAndNeverTheRawToken()
    {
        var cookie = await ManagerCookieAsync();
        var withQr = await _database.SeedTableAsync("1", "Salon");
        var withoutQr = await _database.SeedTableAsync("2");
        await _database.ExecuteAsync(
            "INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status) VALUES ('00000000-0000-0000-0000-000000000001', NULL, 'KASA-1', 0, 'Available');");
        await _database.SeedTunnelAsync(Hostname);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var issued = await IssueAsync(client, cookie, withQr);

        using var response = await client.SendAsync(Request(HttpMethod.Get, ListPath, cookie));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(RawToken(issued), body, StringComparison.Ordinal);
        var rows = JsonSerializer.Deserialize<List<TableQrRowV1>>(body, JsonOptions)!;
        Assert.DoesNotContain(rows, row => row.TableNumber == "KASA-1");
        var salon = Assert.Single(rows, row => row.TableId == withQr);
        Assert.Equal("Salon", salon.ZoneName);
        Assert.NotNull(salon.QrIssuedAt);
        var plain = Assert.Single(rows, row => row.TableId == withoutQr);
        Assert.Null(plain.ZoneName);
        Assert.Null(plain.QrIssuedAt);
    }

    [Fact]
    public async Task AnIssuedAddressOpensACustomerSessionAndARotationClosesTheOldOne()
    {
        var cookie = await ManagerCookieAsync();
        var tableId = await _database.SeedTableAsync("1");
        await _database.SeedTunnelAsync(Hostname);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var first = await IssueAsync(client, cookie, tableId);
        Assert.StartsWith($"https://{Hostname}/qr/?t=", first.QrUrl, StringComparison.Ordinal);
        using (var session = await PostSessionAsync(client, RawToken(first)))
        {
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            Assert.Equal(tableId, (await session.Content.ReadFromJsonAsync<QrSessionIssueResponse>())!.TableId);
        }

        using var rotateResponse = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "rotate"), cookie));
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        var second = (await rotateResponse.Content.ReadFromJsonAsync<TableQrCodeV1>())!;

        using (var oldSession = await PostSessionAsync(client, RawToken(first)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
            Assert.Equal("QR_TOKEN_REVOKED", await ErrorCodeAsync(oldSession));
        }

        using var newSession = await PostSessionAsync(client, RawToken(second));
        Assert.Equal(HttpStatusCode.OK, newSession.StatusCode);
        Assert.Equal(1, await _database.ActiveTokenCountAsync(tableId));
    }

    [Fact]
    public async Task RevokingClosesTheQrAndTheTableShowsNoQrAfterwards()
    {
        var cookie = await ManagerCookieAsync();
        var tableId = await _database.SeedTableAsync("1");
        await _database.SeedTunnelAsync(Hostname);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var issued = await IssueAsync(client, cookie, tableId);

        using var revoke = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "revoke"), cookie));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var session = await PostSessionAsync(client, RawToken(issued));
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        using var list = await client.SendAsync(Request(HttpMethod.Get, ListPath, cookie));
        var rows = (await list.Content.ReadFromJsonAsync<List<TableQrRowV1>>(JsonOptions))!;
        Assert.Null(Assert.Single(rows, row => row.TableId == tableId).QrIssuedAt);
        Assert.Equal(0, await _database.ActiveTokenCountAsync(tableId));
    }

    [Fact]
    public async Task IssuingTwiceIsAConflictAndRotatingOrRevokingWithoutAQrIsNotFound()
    {
        var cookie = await ManagerCookieAsync();
        var tableId = await _database.SeedTableAsync("1");
        await _database.SeedTunnelAsync(Hostname);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using (var rotate = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "rotate"), cookie)))
        {
            Assert.Equal(HttpStatusCode.NotFound, rotate.StatusCode);
            Assert.Equal("QR_NOT_FOUND", await ErrorCodeAsync(rotate));
        }

        using (var revoke = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "revoke"), cookie)))
        {
            Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
            Assert.Equal("QR_NOT_FOUND", await ErrorCodeAsync(revoke));
        }

        await IssueAsync(client, cookie, tableId);
        using var second = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "issue"), cookie));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("QR_ALREADY_EXISTS", await ErrorCodeAsync(second));
    }

    [Fact]
    public async Task AnUnknownTableAndTheQuickSaleAnchorAreNotFound()
    {
        var cookie = await ManagerCookieAsync();
        await _database.ExecuteAsync(
            "INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status) VALUES ('00000000-0000-0000-0000-000000000001', NULL, 'KASA-1', 0, 'Available');");
        await _database.SeedTunnelAsync(Hostname);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        foreach (var tableId in new[] { Guid.NewGuid(), Guid.Parse("00000000-0000-0000-0000-000000000001") })
        {
            using var response = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "issue"), cookie));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("TABLE_NOT_FOUND", await ErrorCodeAsync(response));
        }
    }

    [Fact]
    public async Task WithoutAnActivatedRelayNoTokenIsIssuedAndTheMessageIsTurkish()
    {
        var cookie = await ManagerCookieAsync();
        var tableId = await _database.SeedTableAsync("1");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "issue"), cookie));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("RELAY_NOT_READY", error.GetProperty("code").GetString());
        Assert.Contains("etkinleştirilmedi", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, await _database.ActiveTokenCountAsync(tableId));
    }

    private const string ListPath = "/api/v1/management/qr-tables";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] RateLimitPolicies = ["qr-session", "qr-order", "qr-public"];

    private static string ActionPath(Guid tableId, string action) => $"{ListPath}/{tableId:D}/{action}";

    private static string RawToken(TableQrCodeV1 code) =>
        HttpUtility.ParseQueryString(new Uri(code.QrUrl).Query)["t"]!;

    private Task<string> ManagerCookieAsync() =>
        _database.SeedManagementSessionAsync("manager", Guid.NewGuid(), "manager", "integrations.manage");

    private static HttpRequestMessage Request(HttpMethod method, string path, string? cookie)
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static async Task<TableQrCodeV1> IssueAsync(HttpClient client, string cookie, Guid tableId)
    {
        using var response = await client.SendAsync(Request(HttpMethod.Post, ActionPath(tableId, "issue"), cookie));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TableQrCodeV1>())!;
    }

    private static Task<HttpResponseMessage> PostSessionAsync(HttpClient client, string rawToken) =>
        client.PostAsJsonAsync("/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, Guid.NewGuid(), DateTimeOffset.UtcNow));

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddRateLimiter(options =>
        {
            foreach (var policy in RateLimitPolicies)
                options.AddPolicy(policy, _ => RateLimitPartition.GetNoLimiter("test"));
        });
        builder.Services.AddRelaySettingsExperience();
        RelaySettingsTestHost.AddRelayCredentialChain(builder.Services);
        builder.Services.AddQrOrderingExperience();

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapRelaySettingsApi();
        app.MapQrOrderingApi();
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
