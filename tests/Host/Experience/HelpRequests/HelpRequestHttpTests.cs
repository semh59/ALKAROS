using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.WaiterNotifications;
using Microsoft.AspNetCore.SignalR;
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

    /// <summary>
    /// Found in an independent review (2026-09-11): the cooldown's own read
    /// (last request time) and the insert used to have nothing tying them
    /// together, so two genuinely concurrent raises for the same table
    /// (a real double-tap, or two devices at once) could both read "no
    /// recent request" and both succeed - exactly the scenario this
    /// cooldown's own doc comment says it exists to protect against.
    /// HelpRequestStore.RaiseAsync now takes a transaction-scoped advisory
    /// lock keyed by table id before either read, serializing the two
    /// concurrent calls below into a real first-and-second rather than two
    /// parallel winners. Verified this test genuinely exercises the race
    /// (not a vacuous pass either way): temporarily reverting the lock to a
    /// no-op made this exact test fail 5/5 runs.
    /// </summary>
    [Fact]
    public async Task TwoGenuinelyConcurrentRequestsForTheSameTableProduceExactlyOneWinner()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var firstClient = CreateClient(app);
        using var secondClient = CreateClient(app);

        var responses = await Task.WhenAll(
            firstClient.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
                new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill))),
            secondClient.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
                new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill))));
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == (HttpStatusCode)429));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

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

    [Fact]
    public async Task AnEmptyTableIdIsRejectedBeforeAnythingIsStored()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(Guid.Empty, HelpRequestTypeCatalog.Other)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _database.HelpRequestCountAsync());
    }

    [Fact]
    public async Task TheCooldownRejectionTellsTheCallerHowLongToWait()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var first = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));

        using var second = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));

        Assert.Equal((HttpStatusCode)429, second.StatusCode);
        var seconds = (int)second.Headers.RetryAfter!.Delta!.Value.TotalSeconds;
        Assert.InRange(seconds, 1, 120);
        Assert.Contains($"{seconds} saniye", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheAnswerSaysHowManyManagersWereConnectedWhenTheCallWentOut()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (firstTable, _) = await _database.SeedTableAsync();
        var (secondTable, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var alone = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(firstTable, HelpRequestTypeCatalog.Spill)));
        app.Services.GetRequiredService<HelpRequestRecipientTracker>().Connected("manager-connection");
        using var heard = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cookie,
            new HelpRequestV1(secondTable, HelpRequestTypeCatalog.Spill)));

        Assert.Equal(0, (await alone.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.ConnectedRecipients);
        Assert.Equal(1, (await heard.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.ConnectedRecipients);
    }

    [Fact]
    public async Task AnUnansweredCallStaysListedForManagersUntilItIsAcknowledged()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashierCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, managerToken) = await _database.SeedManagementSessionAsync("manager");
        var managerCookie = $"alkaros.manager={managerToken}";
        var (tableId, tableNumber) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var raised = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Complaint)));
        var helpRequestId = (await raised.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.HelpRequestId;

        var listed = await ListOpenAsync(client, terminalId, managerCookie);
        var open = Assert.Single(listed);
        Assert.Equal(helpRequestId, open.HelpRequestId);
        Assert.Equal(tableNumber, open.TableNumber);

        using var acknowledged = await client.SendAsync(Post(AcknowledgePath(terminalId, helpRequestId), managerCookie));
        Assert.Equal(HttpStatusCode.NoContent, acknowledged.StatusCode);
        Assert.Empty(await ListOpenAsync(client, terminalId, managerCookie));
    }

    [Fact]
    public async Task AcknowledgingTwiceIsHarmlessAndAnUnknownCallIsNotFound()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashierCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, managerToken) = await _database.SeedManagementSessionAsync("supervisor");
        var managerCookie = $"alkaros.manager={managerToken}";
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var raised = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Other)));
        var helpRequestId = (await raised.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.HelpRequestId;

        using var first = await client.SendAsync(Post(AcknowledgePath(terminalId, helpRequestId), managerCookie));
        using var second = await client.SendAsync(Post(AcknowledgePath(terminalId, helpRequestId), managerCookie));
        using var unknown = await client.SendAsync(Post(AcknowledgePath(terminalId, Guid.NewGuid()), managerCookie));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task OnlyAManagerSessionCanListOrAcknowledge()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashierCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var raised = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Other)));
        var helpRequestId = (await raised.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.HelpRequestId;

        using var anonymousList = await client.GetAsync($"{HelpRequestPath(terminalId)}/open");
        using var cashierList = await client.SendAsync(Get($"{HelpRequestPath(terminalId)}/open", cashierCookie));
        using var cashierAcknowledge = await client.SendAsync(Post(AcknowledgePath(terminalId, helpRequestId), cashierCookie));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, cashierList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, cashierAcknowledge.StatusCode);
        Assert.Equal(0, await _database.AcknowledgedCountAsync());
    }

    [Fact]
    public async Task ATableThatWasAnsweredCanCallAgainButAnOpenCallBlocksARepeat()
    {
        var terminalId = Guid.NewGuid();
        var (_, cashierCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, managerToken) = await _database.SeedManagementSessionAsync("manager");
        var managerCookie = $"alkaros.manager={managerToken}";
        var (tableId, _) = await _database.SeedTableAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var first = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));
        var helpRequestId = (await first.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.HelpRequestId;

        using var blocked = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));
        using var acknowledged = await client.SendAsync(Post(AcknowledgePath(terminalId, helpRequestId), managerCookie));
        using var again = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Complaint)));

        Assert.Equal((HttpStatusCode)429, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, await _database.HelpRequestCountAsync());
    }

    [Fact]
    public async Task AcknowledgingTellsOnlyTheWaiterWhoRaisedTheCall()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, cashierCookie) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, managerToken) = await _database.SeedManagementSessionAsync("manager");
        var (tableId, tableNumber) = await _database.SeedTableAsync();
        var waiterHub = new RecordingWaiterHub();
        await using var app = await StartAsync(waiterHub);
        using var client = CreateClient(app);
        using var raised = await client.SendAsync(JsonRequest(HelpRequestPath(terminalId), cashierCookie,
            new HelpRequestV1(tableId, HelpRequestTypeCatalog.Spill)));
        var helpRequestId = (await raised.Content.ReadFromJsonAsync<HelpRequestRaisedV1>())!.HelpRequestId;

        using var response = await client.SendAsync(Post(AcknowledgePath(terminalId, helpRequestId), $"alkaros.manager={managerToken}"));

        var sent = Assert.Single(waiterHub.Sent);
        Assert.Equal(WaiterOrderStatusHub.GroupName(waiterId), sent.Group);
        Assert.Equal(HelpRequestExperience.HelpRequestSeen, sent.Method);
        var seen = Assert.IsType<HelpRequestSeenV1>(sent.Payload);
        Assert.Equal(tableNumber, seen.TableNumber);
        Assert.Equal("Help Request Hub Test", seen.SeenByDisplayName);
    }

    [Fact]
    public async Task OneManagerTakingTheCallAnswersEveryOpenCallOfThatTable()
    {
        var terminalId = Guid.NewGuid();
        var (waiterId, _) = await _database.SeedCashierSessionAsync(terminalId);
        var (_, managerToken) = await _database.SeedManagementSessionAsync("manager");
        var (tableId, tableNumber) = await _database.SeedTableAsync();
        var first = await _database.SeedOpenRequestAsync(tableId, tableNumber, waiterId);
        await _database.SeedOpenRequestAsync(tableId, tableNumber, waiterId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post(AcknowledgePath(terminalId, first), $"alkaros.manager={managerToken}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(2, await _database.AcknowledgedCountAsync());
    }

    private static string AcknowledgePath(Guid terminalId, Guid helpRequestId)
        => $"{HelpRequestPath(terminalId)}/{helpRequestId:D}/acknowledge";

    private static HttpRequestMessage Get(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage Post(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static async Task<IReadOnlyList<OpenHelpRequestV1>> ListOpenAsync(HttpClient client, Guid terminalId, string cookie)
    {
        using var response = await client.SendAsync(Get($"{HelpRequestPath(terminalId)}/open", cookie));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<OpenHelpRequestV1>>())!;
    }

    private static string HelpRequestPath(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/help-requests";

    private static HttpRequestMessage JsonRequest<T>(string path, string cookie, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync(RecordingWaiterHub? waiterHub = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        if (waiterHub is not null)
            builder.Services.AddSingleton<IHubContext<WaiterOrderStatusHub>>(waiterHub);
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

/// <summary>Records every group send; the SignalR group proxy gives no other way to see who was addressed.</summary>
internal sealed class RecordingWaiterHub : IHubContext<WaiterOrderStatusHub>
{
    public List<(string Group, string Method, object? Payload)> Sent { get; } = [];

    public IHubClients Clients => new RecordingClients(Sent);

    public IGroupManager Groups => throw new NotSupportedException();

    private sealed class RecordingClients(List<(string Group, string Method, object? Payload)> sent) : IHubClients
    {
        public IClientProxy Group(string groupName) => new Proxy(groupName, sent);

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class Proxy(string group, List<(string Group, string Method, object? Payload)> sent) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            sent.Add((group, method, args.Single()));
            return Task.CompletedTask;
        }
    }
}
