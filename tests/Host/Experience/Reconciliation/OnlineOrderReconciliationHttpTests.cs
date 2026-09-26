using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// V12-REC-001 over HTTP with the real module composition: only reconciliation.manage may scan, retry or
/// resolve (each writes); every online order source runs against the real schema; a refusal the provider
/// never heard about becomes a case whose retry puts the event back, and a case whose source still
/// diverges cannot be resolved.
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class OnlineOrderReconciliationHttpTests : IAsyncLifetime
{
    private const string BasePath = "/api/v1/management/reconciliation/online-orders";

    private readonly ReconciliationCaseTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        var composition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(builder.Services, composition.Services);
        builder.Services.AddReconciliationCaseExperience();
        builder.Services.AddOnlineOrderReconciliationExperience();

        _application = builder.Build();
        _application.MapReconciliationCaseApi();
        _application.MapOnlineOrderReconciliationApi();
        await _application.StartAsync();
        var addresses = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task OnlyASessionWithManageMayScanRetryOrResolve()
    {
        using var anonymous = CreateClient(null);
        using var denied = CreateClient(ReconciliationCaseTestDatabase.DeniedToken);
        using var viewOnly = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);
        var retry = $"{BasePath}/cases/{Guid.NewGuid()}/retry";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"{BasePath}/scan", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsync($"{BasePath}/scan", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync($"{BasePath}/scan", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync(retry, null)).StatusCode);
        Assert.Equal(0L, await _database.ScalarAsync<long>("SELECT count(*) FROM reconciliation.cases WHERE case_type = 'OnlineOrderMismatch';"));
    }

    [Fact]
    public async Task ARefusedProviderOrderBecomesACaseItsRetryReprocessesAndItResolvesOnlyWhenTheSourceAgrees()
    {
        using var supervisor = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);
        var externalOrderId = "ys-http-" + Guid.NewGuid().ToString("N")[..12];
        var inboxId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.yemeksepeti_webhook_inbox
                (inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope,
                 processed_at, processing_outcome, outcome_detail)
            VALUES (@inbox, lpad(replace(@inbox::text, '-', ''), 64, '0'), @external, 'RECEIVED',
                    lpad(replace(@inbox::text, '-', ''), 64, '0'), '\x00'::bytea, now(), 'Rejected',
                    '{"rejection":"UnmappedSku","providerCancellationRequested":false}'::jsonb);
            """,
            ("inbox", inboxId), ("external", externalOrderId));

        using var scan = await supervisor.PostAsync($"{BasePath}/scan", null);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var results = await scan.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, results.GetArrayLength());
        Assert.All(results.EnumerateArray(), r => Assert.Equal(JsonValueKind.Null, r.GetProperty("failureReason").ValueKind));

        var caseId = await _database.ScalarAsync<Guid>(
            $"SELECT case_id FROM reconciliation.cases WHERE deduplication_key = 'online-order:provider-accepted-locally-refused:{externalOrderId}';");

        using var stillDiverged = await supervisor.PostAsJsonAsync($"{BasePath}/cases/{caseId}/resolve", new { expectedVersion = 1, note = "Eşleme düzeltildi." });
        Assert.Equal(HttpStatusCode.Conflict, stillDiverged.StatusCode);
        Assert.Equal("SOURCE_STILL_DIVERGED", (await stillDiverged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString());

        using var noNote = await supervisor.PostAsJsonAsync($"{BasePath}/cases/{caseId}/resolve", new { expectedVersion = 1, note = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);

        using var retried = await supervisor.PostAsync($"{BasePath}/cases/{caseId}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Requeued", (await retried.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetString());
        Assert.Equal(0L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM online_ordering.yemeksepeti_webhook_inbox WHERE inbox_id = '{inboxId}' AND processed_at IS NOT NULL;"));

        // Intake then creates the order: the provider and the restaurant agree again.
        await _database.ExecuteAsync(
            """
            INSERT INTO orders.orders (order_id, source, source_external_id, status, confirmation_status, order_number, created_at, updated_at)
            VALUES (gen_random_uuid(), 'Online', @external, 'Accepted', 'Accepted', @number, now(), now());
            """,
            ("external", externalOrderId), ("number", "YS-" + externalOrderId));
        using var resolved = await supervisor.PostAsJsonAsync($"{BasePath}/cases/{caseId}/resolve", new { expectedVersion = 1, note = "Eşleme düzeltildi, sipariş oluştu." });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(JsonValueKind.String, (await resolved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resolvedAt").ValueKind);
        Assert.Equal("Resolved", await _database.ScalarAsync<string>($"SELECT status FROM reconciliation.cases WHERE case_id = '{caseId}';"));

        using var afterClose = await supervisor.PostAsync($"{BasePath}/cases/{caseId}/retry", null);
        Assert.Equal(HttpStatusCode.Conflict, afterClose.StatusCode);
        using var unknown = await supervisor.PostAsync($"{BasePath}/cases/{Guid.NewGuid()}/retry", null);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    private HttpClient CreateClient(string? managerToken)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (managerToken is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ReconciliationCaseEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }
}
