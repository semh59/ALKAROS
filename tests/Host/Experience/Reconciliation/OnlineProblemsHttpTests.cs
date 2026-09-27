using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.OnlineOrders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// V12-OUI-006 over HTTP with the real module composition: the online food screen's Problems tab on the terminal's own
/// staff session — only online order cases, viewed with reports.view, retried and resolved with reconciliation.manage
/// through the same actions as the reconciliation console.
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class OnlineProblemsHttpTests : IAsyncLifetime
{
    private const string ManagerCookie = "manager-cashier-token";
    private const string ViewerCookie = "viewer-cashier-token";
    private const string NoRightsCookie = "no-rights-cashier-token";

    private readonly ReconciliationCaseTestDatabase _database = new();
    private readonly Guid _terminalId = Guid.NewGuid();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _database.ExecuteAsync(
            """
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (gen_random_uuid(), @manager, @device, @manager_hash, now(), now() + interval '1 hour'),
                   (gen_random_uuid(), @viewer, @device, @viewer_hash, now(), now() + interval '1 hour'),
                   (gen_random_uuid(), @none, @device, @none_hash, now(), now() + interval '1 hour');
            """,
            ("manager", ReconciliationCaseTestDatabase.SupervisorUserId), ("viewer", ReconciliationCaseTestDatabase.ViewOnlyUserId),
            ("none", ReconciliationCaseTestDatabase.DeniedUserId), ("device", $"cashier:{_terminalId:D}"),
            ("manager_hash", DeviceSessionToken.Hash(ManagerCookie)), ("viewer_hash", DeviceSessionToken.Hash(ViewerCookie)),
            ("none_hash", DeviceSessionToken.Hash(NoRightsCookie)));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        var composition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(builder.Services, composition.Services);
        builder.Services.AddReconciliationCaseExperience();
        builder.Services.AddOnlineOrderReconciliationExperience();
        builder.Services.AddOnlineProblemsExperience();
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy("terminal-read", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("r"));
            limiter.AddPolicy("terminal-write", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w"));
        });

        _application = builder.Build();
        _application.UseRateLimiter();
        _application.MapReconciliationCaseApi();
        _application.MapOnlineProblemsApi();
        await _application.StartAsync();
        _baseAddress = new Uri(Assert.Single(_application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses));
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    private string Root => $"/api/v1/terminals/{_terminalId:D}/online-problems";

    private HttpClient Client(string? cookie)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (cookie is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{DualScreenApplication.CashierCookieName}={cookie}");
        return client;
    }

    private async Task<string> RefusedOrderAsync()
    {
        var externalOrderId = "ys-p-" + Guid.NewGuid().ToString("N")[..12];
        var inboxId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.provider_inbox
                (provider, inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope,
                 processed_at, processing_outcome, outcome_detail)
            VALUES ('yemeksepeti', @inbox, lpad(replace(@inbox::text, '-', ''), 64, '0'), @external, 'RECEIVED',
                    lpad(replace(@inbox::text, '-', ''), 64, '0'), '\x00'::bytea, now(), 'Rejected',
                    '{"rejection":"UnmappedSku","providerCancellationRequested":false}'::jsonb);
            """,
            ("inbox", inboxId), ("external", externalOrderId));
        await using var scope = _application!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OnlineOrderReconciliationScanner>().ScanAllAsync();
        return externalOrderId;
    }

    private static async Task<(string? Code, string? Message)> ErrorAsync(HttpResponseMessage response)
    {
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        return (error.GetProperty("code").GetString(), error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task StaffWhoSeeReportsListOnlyOnlineOrderCasesAndOnlyAReconciliationManagerActs()
    {
        var externalOrderId = await RefusedOrderAsync();
        // A case of another type is not an online problem, even when its details look like one.
        using (var console = new HttpClient { BaseAddress = _baseAddress })
        {
            console.DefaultRequestHeaders.Add("Cookie", $"{ReconciliationCaseEndpoints.ManagerCookieName}={ReconciliationCaseTestDatabase.SupervisorToken}");
            (await console.PostAsJsonAsync("/api/v1/management/reconciliation/cases",
                new CreateReconciliationCaseV1("payment:" + Guid.NewGuid().ToString("N"), CaseType.PaymentMismatch, "a", "b", 10m, CaseSeverity.Low,
                    "{\"kind\":\"ProviderEventFailed\",\"nextAction\":\"ReprocessProviderEvent\"}")))
                .EnsureSuccessStatusCode();
        }

        using var anonymous = Client(null);
        using var noRights = Client(NoRightsCookie);
        using var viewer = Client(ViewerCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Root)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRights.GetAsync(Root)).StatusCode);

        var problems = await viewer.GetFromJsonAsync<JsonElement>(Root);
        var problem = Assert.Single(problems.EnumerateArray());
        Assert.Equal(("ProviderAcceptedLocallyRefused", "yemeksepeti", externalOrderId, "ReprocessProviderEvent", true),
            (problem.GetProperty("kind").GetString(), problem.GetProperty("provider").GetString(), problem.GetProperty("externalOrderId").GetString(),
             problem.GetProperty("nextAction").GetString(), problem.GetProperty("canRetry").GetBoolean()));

        var caseId = problem.GetProperty("caseId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{Root}/{caseId}/retry", null)).StatusCode);

        // A case with nothing to retry (a polling failure streak: check the connection) says so.
        await _database.ExecuteAsync(
            "INSERT INTO online_ordering.provider_poll_state (provider, consecutive_failures, failing_since, last_error) VALUES ('trendyol-go', 3, now(), 'HttpRequestException');");
        await using (var scope = _application!.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<OnlineOrderReconciliationScanner>().ScanAllAsync();
        var polling = Assert.Single((await viewer.GetFromJsonAsync<JsonElement>(Root)).EnumerateArray(), p => p.GetProperty("kind").GetString() == "ProviderPollingFailing");
        Assert.Equal(("CheckChannelConnection", false, "trendyol-go"),
            (polling.GetProperty("nextAction").GetString(), polling.GetProperty("canRetry").GetBoolean(), polling.GetProperty("provider").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Root}/{caseId}/resolve", new { expectedVersion = 1, note = "x" })).StatusCode);
    }

    [Fact]
    public async Task AManagerRetriesTheSafeActionAndCanResolveOnlyOnceTheSourceAgrees()
    {
        var externalOrderId = await RefusedOrderAsync();
        using var manager = Client(ManagerCookie);
        var problem = Assert.Single((await manager.GetFromJsonAsync<JsonElement>(Root)).EnumerateArray());
        var caseId = problem.GetProperty("caseId").GetGuid();
        var version = problem.GetProperty("rowVersion").GetInt32();

        using var noNote = await manager.PostAsJsonAsync($"{Root}/{caseId}/resolve", new { expectedVersion = version, note = "  " });
        Assert.Equal((HttpStatusCode.BadRequest, "Nasıl çözüldüğünü anlatan bir not yazın."), (noNote.StatusCode, (await ErrorAsync(noNote)).Message));
        using var diverged = await manager.PostAsJsonAsync($"{Root}/{caseId}/resolve", new { expectedVersion = version, note = "Eşleme düzeltildi." });
        Assert.Equal((HttpStatusCode.Conflict, "Sorun henüz ortadan kalkmadı; önce önerilen eylemi uygulayın."),
            (diverged.StatusCode, (await ErrorAsync(diverged)).Message));

        using var retry = await manager.PostAsync($"{Root}/{caseId}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Requeued", (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString());
        Assert.Equal(0L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM online_ordering.provider_inbox WHERE external_order_id = '{externalOrderId}' AND processing_outcome IS NOT NULL;"));

        // The event was processed again and became an order: the case can now be closed.
        var orderId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO orders.orders (order_id, source, source_external_id, status, confirmation_status, order_number, created_at, updated_at)
            VALUES (@order, 'Online', @external, 'Accepted', 'Accepted', 'YS-' || @external, now(), now());
            INSERT INTO online_ordering.online_orders (order_id, provider, external_order_id) VALUES (@order, 'yemeksepeti', @external);
            UPDATE online_ordering.provider_inbox SET processed_at = now(), processing_outcome = 'OrderCreated', order_id = @order
            WHERE external_order_id = @external;
            """, ("order", orderId), ("external", externalOrderId));
        var current = Assert.Single((await manager.GetFromJsonAsync<JsonElement>(Root)).EnumerateArray()).GetProperty("rowVersion").GetInt32();
        using var resolved = await manager.PostAsJsonAsync($"{Root}/{caseId}/resolve", new { expectedVersion = current, note = "Eşleme düzeltildi, sipariş oluştu." });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Empty((await manager.GetFromJsonAsync<JsonElement>(Root)).EnumerateArray());

        using var gone = await manager.PostAsync($"{Root}/{Guid.NewGuid()}/retry", null);
        Assert.Equal((HttpStatusCode.NotFound, "Sorun kaydı bulunamadı."), (gone.StatusCode, (await ErrorAsync(gone)).Message));
    }
}
