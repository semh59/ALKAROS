using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.OnlineOrdering.Polling;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.Provider;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Reconciliation.OnlineOrders;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-ONL-009 over real Postgres: a platform's order list is polled into the shared inbox; the cursor moves only
/// after the page is stored, a rate-limited or failing poll waits and loses nothing, one poll per platform runs at
/// a time, an event that also came by webhook is one order, and a failure streak becomes a reconciliation case.
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlineOrderPollingTests : IAsyncLifetime
{
    private const string Platform = "poll-platform";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly ScriptedSource _source = new();
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        secrets.Set(YemeksepetiWebhookInbox.WebhookSecret, "Bearer x");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<ISecretProvider>(secrets);
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddYemeksepetiWebhookExperience();
        OnlineOrderPollingHostedService.AddOnlineOrderPollingExperience(builder.Services);
        builder.Services.AddSingleton<IOnlineOrderPollingSource>(_source);
        builder.Services.AddTransient<IOnlineOrderProvider>(services =>
            new PollPlatform(ActivatorUtilities.CreateInstance<YemeksepetiOnlineOrderProvider>(services)));
        _app = builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private OnlineOrderPoller Poller => _app!.Services.GetRequiredService<OnlineOrderPoller>();

    private YemeksepetiOrderIntakeService Intake => _app!.Services.GetRequiredService<YemeksepetiOrderIntakeService>();

    private static string Key(string orderId, string status, string updatedAt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{orderId}|{status}|{updatedAt}"))).ToLowerInvariant();

    private static PolledOrderEvent Received(string orderId, string sku, string updatedAt = "t1") => new(
        Key(orderId, "RECEIVED", updatedAt), orderId, "RECEIVED", updatedAt, Encoding.UTF8.GetBytes(
            "{\"order_id\":\"" + orderId + "\",\"external_order_id\":\"PO-" + orderId[..6] + "\",\"status\":\"RECEIVED\","
            + "\"transport_type\":\"LOGISTICS_DELIVERY\",\"items\":[{\"_id\":\"i1\",\"sku\":\"" + sku
            + "\",\"pricing\":{\"pricing_type\":\"UNIT\",\"quantity\":1,\"unit_price\":150.00}}],\"sys\":{\"updated_at\":\"" + updatedAt + "\"}}"));

    private async Task<(string? Cursor, int Failures, bool FailingSinceSet, string? LastError, double SecondsUntilNext, bool Succeeded)> StateAsync()
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            SELECT poll_cursor, consecutive_failures, failing_since IS NOT NULL, last_error,
                   EXTRACT(EPOCH FROM next_poll_at - now())::float8, last_success_at IS NOT NULL
            FROM online_ordering.provider_poll_state WHERE provider = $1;
            """);
        command.Parameters.AddWithValue(Platform);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetInt32(1), reader.GetBoolean(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetDouble(4), reader.GetBoolean(5));
    }

    private Task<int> MakeDueAsync() => _database.ExecuteAsync(
        "UPDATE online_ordering.provider_poll_state SET next_poll_at = now() - interval '1 second' WHERE provider = @p;", ("p", Platform));

    private async Task<int> DrainAsync()
    {
        var processed = 0;
        while (await Intake.ProcessNextAsync())
            processed++;
        return processed;
    }

    [Fact]
    public async Task PolledOrdersReachTheInboxAndBecomeOrdersAndTheCursorAdvancesOnlyAfterThat()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = Guid.NewGuid().ToString("D");
        _source.Next(new OnlineOrderPollPage([Received(orderId, sku)], "c-1"));

        Assert.Equal(OnlineOrderPollOutcome.Polled, (await Poller.PollDueAsync())[Platform]);
        Assert.Equal(new string?[] { null }, _source.Cursors);
        var state = await StateAsync();
        Assert.Equal(("c-1", 0, true), (state.Cursor, state.Failures, state.Succeeded));
        Assert.InRange(state.SecondsUntilNext, Interval.TotalSeconds - 5, Interval.TotalSeconds + 1);

        Assert.Equal(1, await DrainAsync());
        var order = Assert.Single(await _database.OnlineOrdersAsync(orderId));
        Assert.StartsWith("PP-", order.OrderNumber, StringComparison.Ordinal);

        // Not due yet: the platform is not asked again.
        Assert.Empty(await Poller.PollDueAsync());
        Assert.Single(_source.Cursors);

        // Due: the next poll continues from the stored cursor; a page without a new cursor keeps it.
        await MakeDueAsync();
        _source.Next(new OnlineOrderPollPage([], null));
        await Poller.PollDueAsync();
        Assert.Equal(new string?[] { null, "c-1" }, _source.Cursors);
        Assert.Equal("c-1", (await StateAsync()).Cursor);
    }

    [Fact]
    public async Task AnOrderThatArrivesByWebhookAndByPollingIsOneOrder()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = Guid.NewGuid().ToString("D");
        var polled = Received(orderId, sku);
        var inbox = _app!.Services.GetRequiredService<ProviderInbox>();
        Assert.Equal(ProviderInboxStoreOutcome.Stored, (await inbox.StoreAsync(new ProviderInboxEvent(
            Platform, polled.EventKey, orderId, "RECEIVED", "t1", polled.RawBody))).Outcome);

        _source.Next(new OnlineOrderPollPage([polled], "c-1"));
        await Poller.PollDueAsync();

        Assert.Equal(1, await DrainAsync());
        Assert.Single(await _database.OnlineOrdersAsync(orderId));
        Assert.Single(await _database.InboxAsync(orderId));
    }

    [Fact]
    public async Task ARateLimitedPollWaitsAsLongAsThePlatformAskedAndKeepsTheCursor()
    {
        _source.Next(new OnlineOrderPollPage([], "c-1"));
        await Poller.PollDueAsync();
        await MakeDueAsync();

        _source.Fail(new OnlineOrderPollRateLimitedException(TimeSpan.FromSeconds(120)));
        Assert.Equal(OnlineOrderPollOutcome.RateLimited, (await Poller.PollDueAsync())[Platform]);
        var state = await StateAsync();
        Assert.Equal(("c-1", 1, true, (string?)"RateLimited"), (state.Cursor, state.Failures, state.FailingSinceSet, state.LastError));
        Assert.InRange(state.SecondsUntilNext, 115, 121);

        // A retry-after shorter than the platform's interval never polls faster than the interval.
        await MakeDueAsync();
        _source.Fail(new OnlineOrderPollRateLimitedException(TimeSpan.FromSeconds(1)));
        await Poller.PollDueAsync();
        Assert.InRange((await StateAsync()).SecondsUntilNext, Interval.TotalSeconds - 5, Interval.TotalSeconds + 1);
    }

    [Fact]
    public async Task AFailingPollBacksOffKeepsTheCursorAndOpensACaseAfterAStreakThatTheNextSuccessEnds()
    {
        var pair = new ProviderPollingFailingSourcePair(_database.DataSource);
        _source.Next(new OnlineOrderPollPage([], "c-1"));
        await Poller.PollDueAsync();

        var expectedWaits = new[] { 60d, 120d, 240d };
        for (var i = 0; i < 3; i++)
        {
            await MakeDueAsync();
            _source.Fail(new HttpRequestException("down"));
            Assert.Equal(OnlineOrderPollOutcome.Failed, (await Poller.PollDueAsync())[Platform]);
            var state = await StateAsync();
            Assert.Equal(("c-1", i + 1, (string?)"HttpRequestException"), (state.Cursor, state.Failures, state.LastError));
            Assert.InRange(state.SecondsUntilNext, expectedWaits[i] - 5, expectedWaits[i] + 1);
            if (i < 2)
                Assert.Empty(await pair.ScanAsync());
        }

        var detected = Assert.Single(await pair.ScanAsync());
        Assert.StartsWith(ProviderPollingFailingSourcePair.DeduplicationPrefix + Platform + ":", detected.DeduplicationKey, StringComparison.Ordinal);
        Assert.Contains("\"provider\":\"" + Platform + "\"", detected.DetailsJson, StringComparison.Ordinal);
        Assert.Contains("CheckChannelConnection", detected.DetailsJson, StringComparison.Ordinal);

        // A fourth failure is the same streak, so the same case.
        await MakeDueAsync();
        _source.Fail(new HttpRequestException("down"));
        await Poller.PollDueAsync();
        Assert.Equal(detected.DeduplicationKey, Assert.Single(await pair.ScanAsync()).DeduplicationKey);

        await MakeDueAsync();
        _source.Next(new OnlineOrderPollPage([], "c-2"));
        await Poller.PollDueAsync();
        var healed = await StateAsync();
        Assert.Equal(("c-2", 0, false, (string?)null), (healed.Cursor, healed.Failures, healed.FailingSinceSet, healed.LastError));
        Assert.Empty(await pair.ScanAsync());
    }

    [Fact]
    public async Task AnOverlongCursorIsAFailureAndStoresNothing()
    {
        var (_, sku) = await _database.SeedSellableProductAsync(onHand: 5m);
        var orderId = Guid.NewGuid().ToString("D");
        _source.Next(new OnlineOrderPollPage([Received(orderId, sku)], new string('c', OnlineOrderPoller.MaxCursorLength + 1)));

        Assert.Equal(OnlineOrderPollOutcome.Failed, (await Poller.PollDueAsync())[Platform]);
        Assert.Null((await StateAsync()).Cursor);
        Assert.Empty(await _database.InboxAsync(orderId));
    }

    [Fact]
    public async Task AnUnconfiguredPlatformIsNotAFailureAndEndsAStreak()
    {
        _source.Fail(new HttpRequestException("down"));
        await Poller.PollDueAsync();
        Assert.Equal(1, (await StateAsync()).Failures);

        await MakeDueAsync();
        _source.Fail(new OnlineOrderPollingNotConfiguredException(Platform));
        Assert.Equal(OnlineOrderPollOutcome.NotConfigured, (await Poller.PollDueAsync())[Platform]);
        var state = await StateAsync();
        Assert.Equal((0, false, (string?)null, false), (state.Failures, state.FailingSinceSet, state.LastError, state.Succeeded));
        Assert.InRange(state.SecondsUntilNext, Interval.TotalSeconds - 5, Interval.TotalSeconds + 1);
    }

    [Fact]
    public async Task OnePollPerPlatformRunsAtATime()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _source.Next(new OnlineOrderPollPage([], "c-1"), gate.Task);
        // The first poll creates the state row and holds it while the platform answers.
        var first = Poller.PollDueAsync();
        await _source.Entered.Task;

        var second = await Poller.PollDueAsync();
        Assert.Empty(second);
        gate.SetResult();
        Assert.Equal(OnlineOrderPollOutcome.Polled, (await first)[Platform]);
        Assert.Single(_source.Cursors);
    }

    [Fact]
    public void APlatformHasAtMostOnePollingSource()
    {
        var inbox = _app!.Services.GetRequiredService<ProviderInbox>();
        Assert.Throws<ArgumentException>(() => new OnlineOrderPoller(_database.DataSource, inbox, [_source, new ScriptedSource()]));
    }

    /// <summary>Answers each poll with the next scripted page or failure and records the cursor it was given.</summary>
    private sealed class ScriptedSource : IOnlineOrderPollingSource
    {
        private readonly Queue<(OnlineOrderPollPage? Page, Exception? Failure, Task? Gate)> _script = new();

        public string Provider => Platform;

        public TimeSpan Interval => OnlineOrderPollingTests.Interval;

        public List<string?> Cursors { get; } = [];

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Next(OnlineOrderPollPage page, Task? gate = null) => _script.Enqueue((page, null, gate));

        public void Fail(Exception failure) => _script.Enqueue((null, failure, null));

        public async Task<OnlineOrderPollPage> PollAsync(string? cursor, CancellationToken cancellationToken = default)
        {
            Cursors.Add(cursor);
            Entered.TrySetResult();
            var (page, failure, gate) = _script.Dequeue();
            if (gate is not null)
                await gate;
            return failure is null ? page! : throw failure;
        }
    }

    /// <summary>The platform the polled events belong to: Yemeksepeti's payload reading under its own identity.</summary>
    private sealed class PollPlatform(IOnlineOrderProvider inner) : IOnlineOrderProvider
    {
        public string Provider => Platform;

        public string DisplayName => "Poll Platform";

        public string OrderNumberPrefix => "PP-";

        public StatusMappingResult MapStatus(string externalOrderId, string providerStatus, string rawPayload) =>
            inner.MapStatus(externalOrderId, providerStatus, rawPayload);

        public Task<NormalizationResult> NormalizeAsync(string rawPayload, DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
            inner.NormalizeAsync(rawPayload, receivedAt, cancellationToken);

        public IReadOnlyList<OnlineOrderLineReference> ReadItemReferences(string rawPayload) => inner.ReadItemReferences(rawPayload);

        public Task<OnlineOutboundStatus?> HandoverStatusAsync(
            Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult<OnlineOutboundStatus?>(OnlineOutboundStatus.ReadyForPickup);

        public Task RequestStatusAsync(
            OnlineOrderStatusRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
