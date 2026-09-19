using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.Foundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Observability.Tests;

/// <summary>
/// V1-RMD-251: found by an independent audit (2026-09-18) — IAlertService
/// (V1-ALT-001) and IObservabilityService's health-check surface
/// (V1-OBS-001) had zero HTTP surface. Proves a real HTTP client can raise
/// an alert, deduplicate a second raise onto the same row, walk it through
/// acknowledge/escalate/resolve with a real audit trail, record a health
/// check (and get rejected for an unapproved retention policy), and that
/// the two-tier permission split (reports.view lets a view-only supervisor
/// read, but only observability.manage can mutate) holds.
/// </summary>
[Collection("Observability PostgreSQL HTTP")]
public sealed class ObservabilityHttpTests : IAsyncLifetime
{
    private readonly ObservabilityTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddObservabilityExperience();

        _application = builder.Build();
        _application.MapObservabilityApi();
        await _application.StartAsync();
        var addresses = _application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task MissingAndUnpermissionedSessionsAreRejectedWithoutMutation()
    {
        var request = new RaiseAlertV1("PrinterOffline", AlertSeverity.Warning, "Printer offline", "Kitchen-1 printer is not responding.");

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/management/observability/alerts/raise", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var denied = CreateClient(ObservabilityTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync("/api/v1/management/observability/alerts/raise", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AViewOnlySupervisorCanReadButNotRaiseOrMutateAnAlert()
    {
        using var supervisor = CreateClient(ObservabilityTestDatabase.SupervisorToken);
        using var raised = await supervisor.PostAsJsonAsync(
            "/api/v1/management/observability/alerts/raise",
            new RaiseAlertV1("StockLow", AlertSeverity.Warning, "Low stock", "Flour is running low."));
        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        var result = await raised.Content.ReadFromJsonAsync<AlertRaiseResultV1>();

        using var viewOnly = CreateClient(ObservabilityTestDatabase.ViewOnlyToken);
        using var read = await viewOnly.GetAsync($"/api/v1/management/observability/alerts/{result!.Alert.AlertId:D}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var mutateAttempt = await viewOnly.PostAsJsonAsync(
            $"/api/v1/management/observability/alerts/{result.Alert.AlertId:D}/acknowledge",
            new AlertActionV1(result.Alert.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, mutateAttempt.StatusCode);
    }

    [Fact]
    public async Task RaisingWithTheSameDeduplicationKeyReturnsTheSameAlert()
    {
        using var client = CreateClient(ObservabilityTestDatabase.SupervisorToken);
        var request = new RaiseAlertV1(
            "PaymentGatewayTimeout", AlertSeverity.Critical, "Payment gateway timeout", "Token terminal did not respond.",
            DeduplicationKey: "RMD251-DEDUP");

        using var first = await client.PostAsJsonAsync("/api/v1/management/observability/alerts/raise", request);
        var firstResult = await first.Content.ReadFromJsonAsync<AlertRaiseResultV1>();
        Assert.True(firstResult!.IsNewAlert);
        Assert.False(firstResult.WasDeduplicated);

        using var second = await client.PostAsJsonAsync("/api/v1/management/observability/alerts/raise", request);
        var secondResult = await second.Content.ReadFromJsonAsync<AlertRaiseResultV1>();
        Assert.Equal(firstResult.Alert.AlertId, secondResult!.Alert.AlertId);
        Assert.True(secondResult.WasDeduplicated);
    }

    [Fact]
    public async Task AcknowledgingEscalatingAndResolvingAnAlertBuildsARealAuditTrail()
    {
        using var client = CreateClient(ObservabilityTestDatabase.SupervisorToken);
        using var raised = await client.PostAsJsonAsync(
            "/api/v1/management/observability/alerts/raise",
            new RaiseAlertV1("KitchenPrinterJam", AlertSeverity.Critical, "Kitchen printer jam", "Kitchen-1 printer is jammed."));
        var opened = (await raised.Content.ReadFromJsonAsync<AlertRaiseResultV1>())!.Alert;

        using var acknowledged = await client.PostAsJsonAsync(
            $"/api/v1/management/observability/alerts/{opened.AlertId:D}/acknowledge",
            new AlertActionV1(opened.RowVersion, "Looking into it."));
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        var acknowledgedAlert = await acknowledged.Content.ReadFromJsonAsync<AlertV1>();
        Assert.Equal(AlertStatus.Acknowledged, acknowledgedAlert!.Status);

        using var resolved = await client.PostAsJsonAsync(
            $"/api/v1/management/observability/alerts/{opened.AlertId:D}/resolve",
            new ResolveAlertV1(acknowledgedAlert.RowVersion, "Printer paper replaced."));
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var resolvedAlert = await resolved.Content.ReadFromJsonAsync<AlertV1>();
        Assert.Equal(AlertStatus.Resolved, resolvedAlert!.Status);

        using var wrongVersion = await client.PostAsJsonAsync(
            $"/api/v1/management/observability/alerts/{opened.AlertId:D}/escalate",
            new AlertActionV1(opened.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, wrongVersion.StatusCode);

        using var eventsResponse = await client.GetAsync($"/api/v1/management/observability/alerts/{opened.AlertId:D}/events");
        Assert.Equal(HttpStatusCode.OK, eventsResponse.StatusCode);
        var events = await eventsResponse.Content.ReadFromJsonAsync<AlertEventV1[]>();
        Assert.Contains(events!, e => e.EventType == AlertEventType.Acknowledged);
        Assert.Contains(events!, e => e.EventType == AlertEventType.Resolved);
    }

    [Fact]
    public async Task RecordingAHealthCheckWithAnUnapprovedRetentionPolicyIsRejected()
    {
        using var client = CreateClient(ObservabilityTestDatabase.SupervisorToken);
        using var response = await client.PostAsJsonAsync(
            "/api/v1/management/observability/health-checks",
            new RecordHealthCheckV1("Database", "PrimaryPostgres", HealthStatus.Healthy, "NOT_AN_APPROVED_POLICY"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordingAHealthCheckWithAnApprovedRetentionPolicySucceedsAndIsQueryable()
    {
        using var client = CreateClient(ObservabilityTestDatabase.SupervisorToken);
        using var created = await client.PostAsJsonAsync(
            "/api/v1/management/observability/health-checks",
            new RecordHealthCheckV1("Database", "PrimaryPostgres-RMD251", HealthStatus.Unhealthy, RetentionPolicyCatalog.HotOperational7D));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await created.Content.ReadFromJsonAsync<HealthCheckV1>();
        Assert.Equal(HealthStatus.Unhealthy, record!.Status);

        using var unhealthy = await client.GetAsync("/api/v1/management/observability/health-checks/unhealthy");
        Assert.Equal(HttpStatusCode.OK, unhealthy.StatusCode);
        var unhealthyChecks = await unhealthy.Content.ReadFromJsonAsync<HealthCheckV1[]>();
        Assert.Contains(unhealthyChecks!, c => c.HealthCheckId == record.HealthCheckId);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ObservabilityEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("Observability PostgreSQL HTTP", DisableParallelization = true)]
public sealed class ObservabilityPostgresqlDefinition;
