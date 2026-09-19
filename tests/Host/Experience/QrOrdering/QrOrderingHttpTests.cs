using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.QrOrdering.PendingOrders;
using ALKAROS.Settings.BusinessIdentity;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.QrOrdering.Tests;

/// <summary>
/// V12-CWB-001: the relay-facing session-issue and menu-read endpoints.
/// Unlike NFC's own HTTP tests, every request here presents either a raw
/// table token or a customer session token — there is no LAN-only trust
/// shortcut on this surface.
/// </summary>
[Collection("QR ordering PostgreSQL HTTP")]
public sealed class QrOrderingHttpTests : IAsyncLifetime
{
    private readonly QrOrderingTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnActiveTableTokenIssuesASessionScopedToItsTable()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrSessionIssueResponse>();
        Assert.Equal(tableId, body!.TableId);
        Assert.False(string.IsNullOrWhiteSpace(body.SessionToken));
        Assert.Equal(1, await _database.NonceCountAsync());
    }

    [Fact]
    public async Task AnUnknownTableTokenIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, "alkaros-table-token:does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("QR kodu tanınmadı", text);
        Assert.DoesNotContain("NOT_FOUND", text);
    }

    [Fact]
    public async Task ARevokedTableTokenIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedRevokedTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("artık geçerli değil", text);
    }

    [Fact]
    public async Task AnExpiredTableTokenIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedExpiredTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await PostSessionAsync(client, rawToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("süresi doldu", text);
    }

    [Fact]
    public async Task AStaleRequestTimestampIsRejected()
    {
        // V12-QRS-002: RelayRequestValidator.DefaultTimestampWindow is 2
        // minutes either side of server time.
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions",
            new QrSessionIssueRequest(rawToken, Guid.NewGuid(), DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("zaman aşımına", text);
    }

    [Fact]
    public async Task ReplayingTheExactSameRequestIsRejectedTheSecondTime()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var nonce = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        using var first = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, nonce, timestamp));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var replay = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, nonce, timestamp));

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var text = await replay.Content.ReadAsStringAsync();
        Assert.Contains("zaten işlendi", text);
    }

    [Fact]
    public async Task AnEmptyTableTokenIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/qr/sessions", new QrSessionIssueRequest(string.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("QR kodu okunamadı", text);
        Assert.DoesNotContain("cannot be", text);
    }

    [Fact]
    public async Task AValidSessionListsAvailableProductsOnTheMenu()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await _database.SeedProductAsync("Mercimek Çorbası", 70m);
        await _database.SeedProductAsync("Izgara Köfte", 320m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<CatalogProductDto>>();
        Assert.Equal(2, products!.Count);
        Assert.Contains(products, p => p.Name == "Mercimek Çorbası" && p.UnitPrice == 70m);
    }

    /// <summary>
    /// A manager-suspended ("86'd") product must not appear on the public
    /// menu either — same real-time toggle V1-RMD-128 fixed for NFC's own
    /// self-service path.
    /// </summary>
    [Fact]
    public async Task AnUnavailableProductIsHiddenFromTheMenu()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await _database.SeedProductAsync("Tükendi", 60m, isAvailable: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        var products = await response.Content.ReadFromJsonAsync<List<CatalogProductDto>>();
        Assert.Empty(products!);
    }

    [Fact]
    public async Task AMissingSessionHeaderIsRejectedWithATurkishMessage()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/api/v1/qr/menu");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Oturumunuz bulunamadı", text);
        Assert.DoesNotContain("NOT_FOUND", text);
    }

    /// <summary>V1-WTR-018: idea #6, "misafir için salt-okunur canlı adisyon".</summary>
    [Fact]
    public async Task ABeforeAnyOrderTheBillHasNoActiveOrder()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/bill");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bill = await response.Content.ReadFromJsonAsync<QrLiveBillDto>();
        Assert.False(bill!.HasActiveOrder);
        Assert.Empty(bill.Lines);
        Assert.Equal(0m, bill.Total);
    }

    [Fact]
    public async Task AfterAnOrderTheBillShowsTheLiveLinesAndTotals()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Izgara Köfte", 320m);
        await _database.SeedActiveOrderAsync(tableId, product, "Izgara Köfte", 320m, quantity: 2);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/bill");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bill = await response.Content.ReadFromJsonAsync<QrLiveBillDto>();
        Assert.True(bill!.HasActiveOrder);
        var line = Assert.Single(bill.Lines);
        Assert.Equal("Izgara Köfte", line.Name);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(640m, bill.Total);
    }

    /// <summary>
    /// Found in an independent review (2026-09-11): GetLiveBillAsync's
    /// terminal-status guard (the exact property this feature's own commit
    /// message highlights as its security guard against showing a guest a
    /// stale/closed tab) had zero regression coverage. The logic was traced
    /// and found correct, but a future edit could silently break it - this
    /// closes the gap.
    /// </summary>
    [Fact]
    public async Task AClosedOrderNeverShowsAStaleBillEvenThoughTheTablesPointerStillNamesIt()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Izgara Köfte", 320m);
        // current_order_id is normally cleared on close; seeded here with it
        // still set (defensive read-side mirror of that, per this method's
        // own doc comment) so the status check is what's actually exercised.
        await _database.SeedActiveOrderAsync(tableId, product, "Izgara Köfte", 320m, status: "Completed");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/bill");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bill = await response.Content.ReadFromJsonAsync<QrLiveBillDto>();
        Assert.False(bill!.HasActiveOrder);
        Assert.Empty(bill.Lines);
        Assert.Equal(0m, bill.Total);
    }

    [Fact]
    public async Task ACancelledLineNeverAppearsOnTheLiveBillEvenInsideAnOpenOrder()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Izgara Köfte", 320m);
        await _database.SeedActiveOrderAsync(tableId, product, "Izgara Köfte", 320m, itemStatus: "Cancelled");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/bill");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bill = await response.Content.ReadFromJsonAsync<QrLiveBillDto>();
        // The order itself is still open (Submitted), so HasActiveOrder is
        // true - it is only the voided line that must never surface.
        Assert.True(bill!.HasActiveOrder);
        Assert.Empty(bill.Lines);
    }

    [Fact]
    public async Task PollingTheBillWithAnInvalidSessionIsRejected()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/bill");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, "does-not-exist");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownSessionTokenIsRejected()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/menu");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, "does-not-exist");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmittingAnOrderQueuesItAndReservesTheTable()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Izgara Köfte", 320m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);
        var submissionId = Guid.NewGuid();

        using var response = await PostOrderAsync(client, sessionToken, submissionId, product, 2);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrOrderSubmissionResponse>();
        Assert.Equal(submissionId, body!.SubmissionId);
        Assert.Equal(tableId, body.TableId);
        Assert.Equal("Reserved", await _database.GetTableStatusAsync(tableId));
        Assert.Equal(1, await _database.OutboxCountAsync());
    }

    [Fact]
    public async Task RetryingTheSameSubmissionReplaysTheExistingResultInsteadOfQueuingTwice()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Ayran", 20m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);
        var submissionId = Guid.NewGuid();

        using var first = await PostOrderAsync(client, sessionToken, submissionId, product, 1);
        var firstBody = await first.Content.ReadFromJsonAsync<QrOrderSubmissionResponse>();

        using var retry = await PostOrderAsync(client, sessionToken, submissionId, product, 1);
        var retryBody = await retry.Content.ReadFromJsonAsync<QrOrderSubmissionResponse>();

        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal(firstBody!.TableId, retryBody!.TableId);
        // Postgres timestamptz truncates to microsecond precision; the first
        // response's own in-memory value (bound before the insert) keeps
        // .NET's full tick precision, so an exact comparison is fragile —
        // the real idempotency guarantee this test cares about is the
        // single outbox row below, not bit-identical timestamps.
        Assert.True((firstBody.SubmittedAt - retryBody.SubmittedAt).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, await _database.OutboxCountAsync());
    }

    [Fact]
    public async Task AnEmptyOrderIsRejectedWithATurkishMessage()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/qr/orders");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        request.Content = JsonContent.Create(new QrOrderSubmissionRequest([], Guid.NewGuid()));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sipariş kalemleri boş olamaz.", text);
        Assert.DoesNotContain("cannot be", text);
    }

    /// <summary>
    /// V12-QRO-004 (Semih's decision, 2026-09-12: two separate guests should
    /// not be forced to share one phone to order): a second guest at an already-occupied
    /// table submits their own QR order from their own phone/session — this
    /// must succeed exactly like the first guest's did, and leave the table
    /// state untouched (it is already exactly where it should be).
    /// </summary>
    [Fact]
    public async Task ASecondGuestAtAnOccupiedTableSucceeds()
    {
        var tableId = await _database.SeedTableAsync(status: "Occupied");
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Kola", 45m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);
        var submissionId = Guid.NewGuid();

        using var response = await PostOrderAsync(client, sessionToken, submissionId, product, 1);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrOrderSubmissionResponse>();
        Assert.Equal(submissionId, body!.SubmissionId);
        Assert.Equal(tableId, body.TableId);
        Assert.Equal("Occupied", await _database.GetTableStatusAsync(tableId));
        Assert.Equal(1, await _database.OutboxCountAsync());
    }

    /// <summary>
    /// V12-QRO-004: unlike Occupied, a table that is Reserved (an earlier
    /// submission is still awaiting a waiter's confirmation — no order has
    /// been accepted yet) still refuses a second submission outright.
    /// </summary>
    [Fact]
    public async Task AReservedTableStillRefusesTheSubmission()
    {
        var tableId = await _database.SeedTableAsync(status: "Reserved");
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Kola", 45m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);

        using var response = await PostOrderAsync(client, sessionToken, Guid.NewGuid(), product, 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("garsonu çağırın", text);
    }

    [Fact]
    public async Task PollingAnUnmaterializedSubmissionReturnsPending()
    {
        var tableId = await _database.SeedTableAsync();
        var rawToken = await _database.SeedActiveTableTokenAsync(tableId);
        var product = await _database.SeedProductAsync("Baklava", 90m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var sessionToken = await IssueSessionAsync(client, rawToken);
        var submissionId = Guid.NewGuid();
        using var submit = await PostOrderAsync(client, sessionToken, submissionId, product, 1);
        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/qr/orders/{submissionId:D}");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrOrderPollResponse>();
        Assert.Equal("Pending", body!.Status);
        Assert.Null(body.OrderId);
    }

    [Fact]
    public async Task PollingWithAnInvalidSessionIsRejected()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/qr/orders/{Guid.NewGuid():D}");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, "does-not-exist");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BrandingIsReachableWithNoSessionAndDefaultsToTheUnsetPaletteColor()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // No X-Alkaros-Qr-Session header at all — unlike every other route
        // in this group, /branding must not require one (V1-SET-007).
        using var response = await client.GetAsync("/api/v1/qr/branding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrBrandingResponse>();
        Assert.Equal("", body!.BusinessName);
        Assert.Equal(BusinessAccentPalette.Resolve(null).Hex, body.AccentColor);
        Assert.False(body.HasLogo);
    }

    [Fact]
    public async Task BrandingReflectsAnOperatorSettingTheBusinessNameAndColor()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var settings = app.Services.GetRequiredService<ISettingsService>();

        await BusinessNameSetting.EnsureRegisteredAsync(settings);
        var nameRecord = await settings.GetRecordAsync(BusinessNameSetting.Key);
        await settings.SetValueAsync(BusinessNameSetting.Key, "Sahil Cafe", nameRecord!.RowVersion);

        await BusinessAccentThemeSetting.EnsureRegisteredAsync(settings);
        var themeRecord = await settings.GetRecordAsync(BusinessAccentThemeSetting.Key);
        await settings.SetValueAsync(BusinessAccentThemeSetting.Key, "lacivert", themeRecord!.RowVersion);

        using var response = await client.GetAsync("/api/v1/qr/branding");

        var body = await response.Content.ReadFromJsonAsync<QrBrandingResponse>();
        Assert.Equal("Sahil Cafe", body!.BusinessName);
        Assert.Equal("#1B4D7B", body.AccentColor);
    }

    [Fact]
    public async Task BrandingFallsBackToTheDefaultPaletteColorWhenTheStoredThemeIsNotARecognizedKey()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var settings = app.Services.GetRequiredService<ISettingsService>();

        await BusinessAccentThemeSetting.EnsureRegisteredAsync(settings);
        var themeRecord = await settings.GetRecordAsync(BusinessAccentThemeSetting.Key);
        // A real corrupted-data scenario, not a revert-and-confirm: a stale
        // key from a since-shrunk palette (or a hand-edited DB row) must
        // never reach a customer-facing page as-is (V1-SET-007).
        await settings.SetValueAsync(BusinessAccentThemeSetting.Key, "#FF00FF", themeRecord!.RowVersion);

        using var response = await client.GetAsync("/api/v1/qr/branding");

        var body = await response.Content.ReadFromJsonAsync<QrBrandingResponse>();
        Assert.Equal(BusinessAccentPalette.Resolve(null).Hex, body!.AccentColor);
    }

    [Fact]
    public async Task LogoIsNotFoundWhenNoneHasBeenSet()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/api/v1/qr/logo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BrandingReportsHasLogoFalseWhenNoneHasBeenSet()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.GetAsync("/api/v1/qr/branding");

        var body = await response.Content.ReadFromJsonAsync<QrBrandingResponse>();
        Assert.False(body!.HasLogo);
    }

    [Fact]
    public async Task LogoReturnsTheUploadedBytesAndBrandingReflectsItsPresence()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var logoStore = app.Services.GetRequiredService<IBusinessLogoStore>();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        await logoStore.SaveAsync(png, "image/png", CancellationToken.None);

        using var logoResponse = await client.GetAsync("/api/v1/qr/logo");
        Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
        Assert.Equal("image/png", logoResponse.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await logoResponse.Content.ReadAsByteArrayAsync());

        using var brandingResponse = await client.GetAsync("/api/v1/qr/branding");
        var body = await brandingResponse.Content.ReadFromJsonAsync<QrBrandingResponse>();
        Assert.True(body!.HasLogo);
    }

    [Fact]
    public async Task LogoHonorsIfNoneMatchWithA304()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var logoStore = app.Services.GetRequiredService<IBusinessLogoStore>();
        await logoStore.SaveAsync([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png", CancellationToken.None);

        using var first = await client.GetAsync("/api/v1/qr/logo");
        var etag = first.Headers.ETag!.Tag;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/qr/logo");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    private static Task<HttpResponseMessage> PostOrderAsync(
        HttpClient client, string sessionToken, Guid submissionId, Guid productId, int quantity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/qr/orders");
        request.Headers.Add(QrOrderingEndpoints.SessionHeaderName, sessionToken);
        request.Content = JsonContent.Create(new QrOrderSubmissionRequest(
            [new QrOrderSubmissionItemRequest(Guid.NewGuid(), productId, quantity)], submissionId));
        return client.SendAsync(request);
    }

    private static async Task<string> IssueSessionAsync(HttpClient client, string rawToken)
    {
        using var response = await PostSessionAsync(client, rawToken);
        var body = await response.Content.ReadFromJsonAsync<QrSessionIssueResponse>();
        return body!.SessionToken;
    }

    private static Task<HttpResponseMessage> PostSessionAsync(HttpClient client, string rawToken) =>
        client.PostAsJsonAsync("/api/v1/qr/sessions", new QrSessionIssueRequest(rawToken, Guid.NewGuid(), DateTimeOffset.UtcNow));

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        // MapQrOrderingApi requires the "qr-session"/"qr-order" named
        // policies to exist; the real Host (DualScreenApplication) registers
        // them with real windows, this standalone test host just needs them
        // present (same reasoning as AddNfcOrderingExperience's own test).
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("qr-session", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
            options.AddPolicy("qr-order", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
        });
        builder.Services.AddQrOrderingExperience();
        var app = builder.Build();
        app.UseRateLimiter();
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

[CollectionDefinition("QR ordering PostgreSQL HTTP", DisableParallelization = true)]
public sealed class QrOrderingPostgresqlDefinition;
