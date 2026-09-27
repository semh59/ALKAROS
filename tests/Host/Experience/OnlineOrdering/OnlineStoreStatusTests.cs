using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.StoreStatus;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// V12-ONL-011 over real Postgres (and HTTP for the screen's endpoints): a manager's open / closed-today / busy request
/// is kept, audited and delivered with back-off; a timed closure ends by itself where the platform supports it and is
/// opened by ALKAROS where it does not. The platform calls are pinned to the public documents only (UNVERIFIED DRAFT).
/// </summary>
[Collection("Online ordering PostgreSQL")]
public sealed class OnlineStoreStatusTests : IAsyncLifetime, IDisposable
{
    // 2026-09-27 12:00 UTC is 15:00 in Istanbul.
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
    private readonly OnlineOrderingTestDatabase _database = new();
    private readonly FakeChannel _selfReopening = new("yemeksepeti", reopensByItself: true);
    private readonly FakeChannel _reopenedByUs = new("trendyol-go", reopensByItself: false);
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton<TimeProvider>(_time);
        builder.Services.AddSingleton<IOnlineStoreStatusChannel>(_selfReopening);
        builder.Services.AddSingleton<IOnlineStoreStatusChannel>(_reopenedByUs);
        builder.Services.AddOnlineStoreStatusExperience();
        // Delivery is driven by the tests themselves, one pass at a time.
        foreach (var hosted in builder.Services.Where(d => d.ImplementationType == typeof(OnlineStoreStatusHostedService)).ToList())
            builder.Services.Remove(hosted);
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy("terminal-read", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("r"));
            limiter.AddPolicy("terminal-write", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w"));
        });
        _app = builder.Build();
        _app.UseRateLimiter();
        _app.MapOnlineStoreStatusApi();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public void Dispose() => _client?.Dispose();

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private OnlineStoreStatusService Service() => new(_database.DataSource, [_selfReopening, _reopenedByUs], _time);

    private Task<string?> RowAsync(string provider) => _database.ScalarTextAsync(
        $"""
        SELECT desired_state || '|' || coalesce(to_char(closed_until AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-') || '|' ||
               coalesce(close_reason, '-') || '|' || (delivered_at IS NOT NULL) || '|' || delivery_attempts || '|' ||
               coalesce(last_error, '-') || '|' || coalesce(to_char(next_attempt_at AT TIME ZONE 'UTC', 'HH24:MI:SS'), '-')
        FROM online_ordering.platform_store_status WHERE provider = '{provider}';
        """);

    private Task<string?> AuditTrailAsync() => _database.ScalarTextAsync(
        """
        SELECT string_agg(actor_type || ':' || (metadata_json->>'provider') || ':' || (metadata_json->>'state'), ',' ORDER BY occurred_at, actor_type DESC)
        FROM audit.audit_events WHERE event_name = 'OnlinePlatform.StoreStatusRequested' AND aggregate_type = 'OnlinePlatform';
        """);

    [Fact]
    public async Task ABusyPauseIsKeptAuditedAndDeliveredOnce()
    {
        var service = Service();
        var requested = await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.ClosedUntil, 30), _actor);
        Assert.Equal((new DateTimeOffset(2026, 9, 27, 12, 30, 0, TimeSpan.Zero), OnlineStoreCloseReason.Busy, false),
            (requested.ClosedUntil!.Value, requested.Reason!.Value, requested.Delivered));
        Assert.Equal("ClosedUntil|2026-09-27 12:30|Busy|false|0|-|-", await RowAsync("trendyol-go"));
        Assert.Equal("User:trendyol-go:ClosedUntil", await AuditTrailAsync());
        Assert.Equal(_actor.ToString("D"), await _database.ScalarTextAsync(
            "SELECT actor_id::text FROM audit.audit_events WHERE event_name = 'OnlinePlatform.StoreStatusRequested';"));

        Assert.Equal(1, await service.DeliverDueAsync());
        Assert.Equal([(OnlineStoreState.ClosedUntil, new DateTimeOffset(2026, 9, 27, 12, 30, 0, TimeSpan.Zero), (OnlineStoreCloseReason?)OnlineStoreCloseReason.Busy)],
            _reopenedByUs.Applied);
        Assert.Equal("ClosedUntil|2026-09-27 12:30|Busy|true|1|-|-", await RowAsync("trendyol-go"));
        Assert.Equal(0, await service.DeliverDueAsync());
        Assert.Single(_reopenedByUs.Applied);
        Assert.Empty(_selfReopening.Applied);
    }

    [Theory]
    [InlineData(OnlineStoreState.ClosedUntil, null)]
    [InlineData(OnlineStoreState.ClosedUntil, 25)]
    [InlineData(OnlineStoreState.ClosedUntil, 0)]
    [InlineData(OnlineStoreState.ClosedUntil, 180)]
    [InlineData(OnlineStoreState.Open, 30)]
    [InlineData(OnlineStoreState.ClosedToday, 30)]
    public async Task OnlyTheOfferedBusyDurationsAreAccepted(OnlineStoreState state, int? minutes)
    {
        var refused = await Assert.ThrowsAsync<InvalidStoreStatusRequestException>(
            () => Service().RequestAsync(new OnlineStoreStatusRequest("yemeksepeti", state, minutes), _actor));
        Assert.Equal("InvalidDuration", refused.Message);
        Assert.Null(await RowAsync("yemeksepeti"));
        Assert.Null(await AuditTrailAsync());
    }

    [Fact]
    public async Task AnUnsetOrUnknownPlatformOrAMissingActorIsRefused()
    {
        _selfReopening.Configured = false;
        Assert.Equal("NotConfigured", (await Assert.ThrowsAsync<InvalidStoreStatusRequestException>(
            () => Service().RequestAsync(new OnlineStoreStatusRequest("yemeksepeti", OnlineStoreState.ClosedToday, null), _actor))).Message);
        await Assert.ThrowsAsync<UnknownStoreStatusPlatformException>(
            () => Service().RequestAsync(new OnlineStoreStatusRequest("migros-yemek", OnlineStoreState.Open, null), _actor));
        await Assert.ThrowsAsync<InvalidStoreStatusRequestException>(
            () => Service().RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.Open, null), Guid.Empty));
        Assert.Null(await AuditTrailAsync());
    }

    [Theory]
    [InlineData("2026-09-27T20:00:00Z", "2026-09-28T03:00:00Z")] // 23:00 local: until tomorrow 06:00
    [InlineData("2026-09-27T02:59:00Z", "2026-09-27T03:00:00Z")] // 05:59 local: the service day still ends this morning
    [InlineData("2026-09-27T03:00:00Z", "2026-09-28T03:00:00Z")] // 06:00 local: a new service day has begun
    public void TodayEndsAtTheNextSixOClockInIstanbul(string now, string expected)
    {
        var end = OnlineStoreStatusService.EndOfServiceDay(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), end);
        Assert.Equal(TimeSpan.Zero, end.Offset);
    }

    [Fact]
    public async Task ClosingForTodayUsesTheServiceDayAndTheClosedReason()
    {
        await Service().RequestAsync(new OnlineStoreStatusRequest("yemeksepeti", OnlineStoreState.ClosedToday, null), _actor);
        Assert.Equal("ClosedToday|2026-09-28 03:00|Closed|false|0|-|-", await RowAsync("yemeksepeti"));
        await Service().DeliverDueAsync();
        Assert.Equal([(OnlineStoreState.ClosedToday, new DateTimeOffset(2026, 9, 28, 3, 0, 0, TimeSpan.Zero), (OnlineStoreCloseReason?)OnlineStoreCloseReason.Closed)],
            _selfReopening.Applied);
    }

    [Fact]
    public async Task AFailedDeliveryWaitsItsBackOffAndIsRetried()
    {
        var service = Service();
        await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.ClosedUntil, 60), _actor);
        _reopenedByUs.Fail = new HttpRequestException("secret platform text");
        Assert.Equal(0, await service.DeliverDueAsync());
        Assert.Equal("ClosedUntil|2026-09-27 13:00|Busy|false|1|HttpRequestException|12:00:15", await RowAsync("trendyol-go"));

        _time.Advance(TimeSpan.FromSeconds(14));
        Assert.Equal(0, await service.DeliverDueAsync());
        Assert.Single(_reopenedByUs.Applied);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, await service.DeliverDueAsync());
        Assert.Equal("ClosedUntil|2026-09-27 13:00|Busy|false|2|HttpRequestException|12:00:45", await RowAsync("trendyol-go"));

        _reopenedByUs.Fail = null;
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await service.DeliverDueAsync());
        Assert.Equal("ClosedUntil|2026-09-27 13:00|Busy|true|3|-|-", await RowAsync("trendyol-go"));
        Assert.Equal(3, _reopenedByUs.Applied.Count);

        var status = (await service.StatusAsync()).Single(s => s.Provider == "trendyol-go");
        Assert.False(status.DeliveryFailing);
    }

    [Fact]
    public async Task TheBackOffStopsGrowingAtFifteenMinutes()
    {
        var service = Service();
        await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.ClosedToday, null), _actor);
        await _database.ExecAsync("UPDATE online_ordering.platform_store_status SET delivery_attempts = 12;");
        _reopenedByUs.Fail = new InvalidOperationException();
        await service.DeliverDueAsync();
        Assert.EndsWith("|13|InvalidOperationException|12:15:00", await RowAsync("trendyol-go"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANewRequestReplacesAnUndeliveredOne()
    {
        var service = Service();
        await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.ClosedUntil, 30), _actor);
        _reopenedByUs.Fail = new HttpRequestException();
        await service.DeliverDueAsync();
        _time.Advance(TimeSpan.FromSeconds(1));
        await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.Open, null), _actor);
        Assert.Equal("Open|-|-|false|0|-|-", await RowAsync("trendyol-go"));
        _reopenedByUs.Fail = null;
        await service.DeliverDueAsync();
        Assert.Equal((OnlineStoreState.Open, (DateTimeOffset?)null, (OnlineStoreCloseReason?)null), _reopenedByUs.Applied[^1]);
        Assert.Equal("User:trendyol-go:ClosedUntil,User:trendyol-go:Open", await AuditTrailAsync());
    }

    [Fact]
    public async Task AnEndedClosureIsOpenedByUsOnlyWhereThePlatformCannotReopenByItself()
    {
        var service = Service();
        await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.ClosedUntil, 30), _actor);
        await service.RequestAsync(new OnlineStoreStatusRequest("yemeksepeti", OnlineStoreState.ClosedUntil, 30), _actor);
        Assert.Equal(2, await service.DeliverDueAsync());

        _time.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(0, await service.DeliverDueAsync());
        Assert.StartsWith("ClosedUntil|", await RowAsync("trendyol-go"), StringComparison.Ordinal);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await service.DeliverDueAsync());
        Assert.Equal((OnlineStoreState.Open, (DateTimeOffset?)null, (OnlineStoreCloseReason?)null), _reopenedByUs.Applied[^1]);
        Assert.Equal("Open|-|-|true|1|-|-", await RowAsync("trendyol-go"));
        // Yemeksepeti ends CLOSED_UNTIL itself: recorded as open, never called again.
        Assert.Single(_selfReopening.Applied);
        Assert.Equal("Open|-|-|true|0|-|-", await RowAsync("yemeksepeti"));
        Assert.Equal(2, (await _database.ScalarTextAsync(
            "SELECT count(*)::text FROM audit.audit_events WHERE event_name = 'OnlinePlatform.StoreStatusRequested' AND actor_type = 'System' AND actor_id IS NULL;")) is { } n ? int.Parse(n, System.Globalization.CultureInfo.InvariantCulture) : -1);
    }

    [Fact]
    public async Task StatusShowsEachPlatformWithWhatItReportsOrThatItCannotBeRead()
    {
        _selfReopening.Reported = new PlatformStoreState(false, new DateTimeOffset(2026, 9, 27, 13, 0, 0, TimeSpan.Zero));
        _reopenedByUs.Configured = false;
        var service = Service();
        await service.RequestAsync(new OnlineStoreStatusRequest("yemeksepeti", OnlineStoreState.ClosedUntil, 60), _actor);

        var status = (await service.StatusAsync()).ToDictionary(s => s.Provider);
        Assert.Equal("trendyol-go,yemeksepeti", string.Join(",", status.Keys.Order(StringComparer.Ordinal)));
        Assert.Equal((true, OnlineStoreState.ClosedUntil, false, false), (status["yemeksepeti"].Configured, status["yemeksepeti"].DesiredState,
            status["yemeksepeti"].Delivered, status["yemeksepeti"].Platform!.Open));
        // Never asked, not configured: shown as open and settled, and the platform is not called.
        Assert.Equal((false, OnlineStoreState.Open, true, (PlatformStoreState?)null), (status["trendyol-go"].Configured,
            status["trendyol-go"].DesiredState, status["trendyol-go"].Delivered, status["trendyol-go"].Platform));
        Assert.Equal(0, _reopenedByUs.Reads);

        _selfReopening.Reported = null;
        Assert.Null((await service.StatusAsync()).Single(s => s.Provider == "yemeksepeti").Platform);
    }

    /// <summary>
    /// Defense-in-depth for the migration's own constraint, not the C# layer (which never produces one of
    /// these combinations): a row missing either half of a closure (a state other than Open with only one of
    /// closed_until/close_reason set, or Open with a stray reason) must never reach the table, even from a
    /// manual edit or a future bug that bypasses OnlineStoreStatusService entirely.
    /// </summary>
    [Theory]
    [InlineData("ClosedToday", "now() + interval '1 hour'", "NULL")]
    [InlineData("ClosedUntil", "NULL", "'Busy'")]
    [InlineData("Open", "NULL", "'Busy'")]
    public async Task TheClosureConstraintRejectsAStateMissingEitherHalf(string state, string until, string reason)
    {
        await Assert.ThrowsAsync<PostgresException>(() => _database.ExecAsync(
            $"""
            INSERT INTO online_ordering.platform_store_status (provider, desired_state, closed_until, close_reason, requested_at)
            VALUES ('yemeksepeti', '{state}', {until}, {reason}, now());
            """));
        Assert.Null(await RowAsync("yemeksepeti"));
    }

    [Fact]
    public async Task AnUnconfiguredPlatformsPendingRequestWaitsWithoutACall()
    {
        var service = Service();
        await service.RequestAsync(new OnlineStoreStatusRequest("trendyol-go", OnlineStoreState.ClosedToday, null), _actor);
        _reopenedByUs.Configured = false;
        Assert.Equal(0, await service.DeliverDueAsync());
        Assert.Empty(_reopenedByUs.Applied);
        Assert.Equal("ClosedToday|2026-09-28 03:00|Closed|false|0|-|-", await RowAsync("trendyol-go"));
    }

    // ---- HTTP ----

    private string Root => $"/api/v1/terminals/{_terminalId:D}/online-store-status";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? cookie, object? body = null)
    {
        using var request = new HttpRequestMessage(method, Root + path) { Content = body is null ? null : JsonContent.Create(body) };
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await _client!.SendAsync(request);
    }

    private static async Task<(string? Code, string? Message)> ErrorAsync(HttpResponseMessage response)
    {
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        return (error.GetProperty("code").GetString(), error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task OnlyAManagerSeesOrChangesTheSwitch()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/", null)).StatusCode);
        var cashier = await _database.SeedStaffSessionAsync(_terminalId, "orders.create");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/", cashier)).StatusCode);
        using var refused = await SendAsync(HttpMethod.Put, "/yemeksepeti", cashier, new { state = "Busy", minutes = 30 });
        Assert.Equal((HttpStatusCode.Forbidden, "Bu işlem için yetkiniz yok."), (refused.StatusCode, (await ErrorAsync(refused)).Message));
        var otherTerminal = await _database.SeedStaffSessionAsync(Guid.NewGuid(), "integrations.manage");
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/", otherTerminal)).StatusCode);
        Assert.Null(await RowAsync("yemeksepeti"));
    }

    [Fact]
    public async Task AManagerPausesAPlatformAndSeesItPendingThenDelivered()
    {
        var manager = await _database.SeedStaffSessionAsync(_terminalId, "integrations.manage");
        _reopenedByUs.Reported = new PlatformStoreState(true, null);
        using var put = await SendAsync(HttpMethod.Put, "/trendyol-go", manager, new { state = "Busy", minutes = 45 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var view = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(("Busy", "Pending"), (view.GetProperty("state").GetString(), view.GetProperty("delivery").GetString()));
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 12, 45, 0, TimeSpan.Zero), view.GetProperty("closedUntil").GetDateTimeOffset());

        using (var pending = await SendAsync(HttpMethod.Get, "/", manager))
        {
            var rows = (await pending.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToDictionary(r => r.GetProperty("provider").GetString()!);
            Assert.Equal(("Busy", "Pending", "Open"), (rows["trendyol-go"].GetProperty("state").GetString(),
                rows["trendyol-go"].GetProperty("delivery").GetString(), rows["trendyol-go"].GetProperty("platformState").GetString()));
            Assert.Equal(("Open", "Delivered", "Unknown"), (rows["yemeksepeti"].GetProperty("state").GetString(),
                rows["yemeksepeti"].GetProperty("delivery").GetString(), rows["yemeksepeti"].GetProperty("platformState").GetString()));
        }

        _reopenedByUs.Fail = new HttpRequestException("platform said: internal detail");
        await Service().DeliverDueAsync();
        using (var retrying = await SendAsync(HttpMethod.Get, "/", manager))
        {
            var text = await retrying.Content.ReadAsStringAsync();
            Assert.DoesNotContain("HttpRequestException", text, StringComparison.Ordinal);
            Assert.DoesNotContain("internal detail", text, StringComparison.Ordinal);
            var rows = JsonDocument.Parse(text).RootElement.EnumerateArray().ToDictionary(r => r.GetProperty("provider").GetString()!);
            Assert.Equal("Retrying", rows["trendyol-go"].GetProperty("delivery").GetString());
        }

        _reopenedByUs.Fail = null;
        _reopenedByUs.Reported = new PlatformStoreState(false, null);
        _time.Advance(TimeSpan.FromMinutes(1));
        await Service().DeliverDueAsync();
        using var delivered = await SendAsync(HttpMethod.Get, "/", manager);
        var final = (await delivered.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single(r => r.GetProperty("provider").GetString() == "trendyol-go");
        Assert.Equal(("Delivered", "Closed"), (final.GetProperty("delivery").GetString(), final.GetProperty("platformState").GetString()));
    }

    [Fact]
    public async Task EveryRefusalIsInTurkish()
    {
        var manager = await _database.SeedStaffSessionAsync(_terminalId, "integrations.manage");
        using var duration = await SendAsync(HttpMethod.Put, "/yemeksepeti", manager, new { state = "Busy", minutes = 20 });
        Assert.Equal((HttpStatusCode.BadRequest, ("INVALID_DURATION", "Yoğunluk süresi 15, 30, 45, 60, 90 veya 120 dakika olmalı.")),
            (duration.StatusCode, await ErrorAsync(duration)));
        using var state = await SendAsync(HttpMethod.Put, "/yemeksepeti", manager, new { state = "Paused" });
        Assert.Equal((HttpStatusCode.BadRequest, ("VALIDATION_FAILED", "İstek doğrulanamadı.")), (state.StatusCode, await ErrorAsync(state)));
        using var unknown = await SendAsync(HttpMethod.Put, "/migros-yemek", manager, new { state = "Open" });
        Assert.Equal((HttpStatusCode.NotFound, "Bu online platform tanınmıyor."), (unknown.StatusCode, (await ErrorAsync(unknown)).Message));
        _selfReopening.Configured = false;
        using var unset = await SendAsync(HttpMethod.Put, "/yemeksepeti", manager, new { state = "ClosedToday" });
        Assert.Equal((HttpStatusCode.Conflict, "Bu platformun bağlantı bilgileri eksik; önce Ayarlar'dan girin."),
            (unset.StatusCode, (await ErrorAsync(unset)).Message));
        Assert.Null(await RowAsync("yemeksepeti"));
    }

    // ---- Platform calls (UNVERIFIED DRAFT, pinned to the public documents) ----

    [Fact]
    public async Task YemeksepetiIsClosedUntilATimeForAKitchenTooBusyAndOpenedWithoutAReason()
    {
        var handler = new RecordingHandler("{\"status\":\"CLOSED_UNTIL\",\"closed_until\":\"2026-09-27T12:30:00Z\"}");
        var secrets = new InMemorySecretProvider();
        foreach (var (reference, value) in new[]
                 {
                     (YemeksepetiPartnerHttpClient.BaseUrl, "https://sandbox.partner.deliveryhero.io"), (YemeksepetiPartnerHttpClient.ChainId, "chain-42"),
                     (YemeksepetiPartnerHttpClient.VendorId, "v 7"), (YemeksepetiPartnerHttpClient.ClientId, "c"), (YemeksepetiPartnerHttpClient.ClientSecret, "s"),
                 })
            secrets.Set(reference, value);
        using var client = new YemeksepetiPartnerHttpClient(new HttpClient(handler), secrets, _time);
        var channel = new YemeksepetiStoreStatusChannel(client, secrets);
        Assert.True(channel.IsConfigured);

        await channel.ApplyAsync(OnlineStoreState.ClosedUntil, new DateTimeOffset(2026, 9, 27, 15, 30, 0, TimeSpan.FromHours(3)), OnlineStoreCloseReason.Busy);
        await channel.ApplyAsync(OnlineStoreState.ClosedToday, null, OnlineStoreCloseReason.Closed);
        await channel.ApplyAsync(OnlineStoreState.Open, null, null);
        var reported = await channel.ReadAsync();

        var calls = handler.Requests.Where(r => !r.Url.EndsWith("/v2/oauth/token", StringComparison.Ordinal)).ToList();
        Assert.All(calls, c => Assert.Equal("https://sandbox.partner.deliveryhero.io/v2/chains/chain-42/vendors/v%207/status", c.Url));
        Assert.All(calls, c => Assert.Equal("Bearer", c.Authorization));
        Assert.Equal("PUT,PUT,PUT,GET", string.Join(",", calls.Select(c => c.Method)));
        Assert.Equal("{\"status\":\"CLOSED_UNTIL\",\"closed_reason\":\"TOO_BUSY_KITCHEN\",\"closed_until\":\"2026-09-27T12:30:00Z\"}", calls[0].Body);
        Assert.Equal("{\"status\":\"CLOSED_TODAY\",\"closed_reason\":\"CLOSED\"}", calls[1].Body);
        Assert.Equal("{\"status\":\"OPEN\"}", calls[2].Body);
        Assert.Equal(new PlatformStoreState(false, new DateTimeOffset(2026, 9, 27, 12, 30, 0, TimeSpan.Zero)), reported);

        handler.Status = HttpStatusCode.BadGateway;
        await Assert.ThrowsAsync<YemeksepetiPartnerApiException>(() => channel.ApplyAsync(OnlineStoreState.Open, null, null));
        Assert.False(new YemeksepetiStoreStatusChannel(client, new InMemorySecretProvider()).IsConfigured);
    }

    [Fact]
    public async Task TrendyolGoIsSwitchedOpenOrClosedAndItsStoreIsFoundInTheStoreList()
    {
        var handler = new RecordingHandler(
            "{\"restaurants\":[{\"id\":152,\"workingStatus\":\"OPEN\"}],\"totalPages\":2}",
            "{\"restaurants\":[{\"id\":153,\"workingStatus\":\"CLOSED\"}],\"totalPages\":2}");
        var secrets = new InMemorySecretProvider();
        var channel = new TrendyolGoStoreStatusChannel(new HttpClient(handler), secrets);
        Assert.False(channel.IsConfigured);
        foreach (var (reference, value) in new[]
                 {
                     (TrendyolGoApiSettings.BaseUrlReference, "https://stageapi.tgoapis.com"), (TrendyolGoApiSettings.SupplierIdReference, "1"),
                     (TrendyolGoApiSettings.ApiKeyReference, "k"), (TrendyolGoApiSettings.ApiSecretReference, "s"),
                     (TrendyolGoApiSettings.IntegratorNameReference, "ALKAROS"), (TrendyolGoApiSettings.ExecutorEmailReference, "e@example.test"),
                 })
            secrets.Set(reference, value);
        Assert.False(channel.IsConfigured);
        secrets.Set(TrendyolGoMenuClient.StoreIdReference, "153");
        Assert.True(channel.IsConfigured);

        await channel.ApplyAsync(OnlineStoreState.ClosedUntil, _time.GetUtcNow().AddMinutes(30), OnlineStoreCloseReason.Busy);
        await channel.ApplyAsync(OnlineStoreState.Open, null, null);
        Assert.Equal(new PlatformStoreState(false, null), await channel.ReadAsync());

        Assert.Equal(("PUT", "/integrator/store/meal/suppliers/1/stores/153/status", "{\"status\":\"CLOSED\"}"),
            (handler.Requests[0].Method, new Uri(handler.Requests[0].Url).AbsolutePath, handler.Requests[0].Body));
        Assert.Equal("{\"status\":\"OPEN\"}", handler.Requests[1].Body);
        Assert.Equal("/integrator/store/meal/suppliers/1/stores?page=0&size=50,/integrator/store/meal/suppliers/1/stores?page=1&size=50",
            string.Join(",", handler.Requests.Skip(2).Select(r => new Uri(r.Url).PathAndQuery)));
        Assert.All(handler.Requests, r => Assert.Equal("Basic", r.Authorization));

        handler.Status = HttpStatusCode.TooManyRequests;
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => channel.ApplyAsync(OnlineStoreState.Open, null, null));
        await Assert.ThrowsAsync<TrendyolGoApiException>(() => channel.ReadAsync());
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeChannel(string provider, bool reopensByItself) : IOnlineStoreStatusChannel
    {
        public List<(OnlineStoreState State, DateTimeOffset? Until, OnlineStoreCloseReason? Reason)> Applied { get; } = [];

        public Exception? Fail { get; set; }

        public PlatformStoreState? Reported { get; set; }

        public int Reads { get; private set; }

        public bool Configured { get; set; } = true;

        public string Provider => provider;

        public bool IsConfigured => Configured;

        public bool ReopensByItself => reopensByItself;

        public Task ApplyAsync(OnlineStoreState state, DateTimeOffset? until, OnlineStoreCloseReason? reason, CancellationToken cancellationToken = default)
        {
            Applied.Add((state, until, reason));
            return Fail is null ? Task.CompletedTask : Task.FromException(Fail);
        }

        public Task<PlatformStoreState> ReadAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Reported is null ? Task.FromException<PlatformStoreState>(new HttpRequestException()) : Task.FromResult(Reported);
        }
    }

    /// <summary>Answers a token request with a token, every other request with the next queued body (or the last one).</summary>
    private sealed class RecordingHandler(params string[] bodies) : HttpMessageHandler
    {
        private int _next;

        public List<(string Method, string Url, string? Authorization, string Body)> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.Scheme, body));
            if (request.RequestUri.AbsolutePath.EndsWith("/v2/oauth/token", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"tok\",\"token_type\":\"Bearer\",\"expires_in\":7200}", Encoding.UTF8, "application/json"),
                };
            if (Status != HttpStatusCode.OK)
                return new HttpResponseMessage(Status);
            var answer = request.Method == HttpMethod.Get && bodies.Length > 0 ? bodies[Math.Min(_next++, bodies.Length - 1)] : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }
}
