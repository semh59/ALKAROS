using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.HelpRequests.Tests;

/// <summary>
/// V1-WTR-014: a waiter's real-time call for help. This is the first real
/// HTTP caller of HelpRequestStore - proves the table lookup, the 2-minute
/// per-table cooldown, and the reason-catalog validation all actually run
/// end to end. The SignalR broadcast side (HelpRequestHub) is not
/// independently asserted here - IHubContext.Clients.All.SendAsync is
/// framework-owned wiring already covered by WaiterOrderStatusHub's own
/// precedent in this codebase; what this endpoint owns and could get wrong
/// is everything before that call.
/// </summary>
[Collection("Help request PostgreSQL HTTP")]
public sealed class HelpRequestHttpTests : IAsyncLifetime
{
    private readonly HelpRequestTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task RaisingAHelpRequestSucceedsAndIsStored()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (tableId, tableNumber) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Complaint)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HelpRequestedV1>();
        Assert.Equal(tableNumber, body!.TableNumber);
        Assert.Equal(HelpRequestTypeCatalog.Complaint, body.RequestType);
        Assert.Equal("Help Request API Test", body.RequestedByDisplayName);
        Assert.Equal(1, await _database.HelpRequestCountAsync());
    }

    [Fact]
    public async Task ASecondRequestForTheSameTableWithinTheCooldownIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var first = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));
        Assert.Equal((HttpStatusCode)429, second.StatusCode);
        Assert.Equal(1, await _database.HelpRequestCountAsync());
    }

    [Fact]
    public async Task ARequestForADifferentTableDuringAnotherTablesCooldownIsNotAffected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (firstTableId, _) = await _database.SeedTableAsync();
        var (secondTableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var first = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(firstTableId, HelpRequestTypeCatalog.Spill)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(secondTableId, HelpRequestTypeCatalog.Spill)));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2, await _database.HelpRequestCountAsync());
    }

    [Fact]
    public async Task AnInvalidRequestTypeIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(tableId, "NotARealType")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _database.HelpRequestCountAsync());
    }

    [Fact]
    public async Task ARequestForANonexistentTableIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(Guid.NewGuid(), HelpRequestTypeCatalog.Other)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NoSessionCookieIsUnauthorized()
    {
        var terminalId = Guid.NewGuid();
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            HelpRequestPath(terminalId), new HelpRequestV1(tableId, HelpRequestTypeCatalog.Other));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string HelpRequestPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/help-requests";

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
        builder.Services.AddHelpRequestExperience();
        var app = builder.Build();
        // Same reasoning as WebPushHttpTests' own StartAsync: the composed
        // host installs this before mapping any API (DualScreenApplication
        // .Build); without it here this test asserts against a pipeline
        // production does not have.
        DualScreenApplication.UseDualScreenErrorHandling(app);
        app.MapHelpRequestApi();
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

[CollectionDefinition("Help request PostgreSQL HTTP", DisableParallelization = true)]
public sealed class HelpRequestPostgresqlDefinition;
