using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Reconciliation;
using ALKAROS.Reconciliation.CaseFoundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// V1-RMD-250: found by an independent audit (2026-09-18) —
/// IReconciliationService (V1-REC-001) had zero HTTP surface. Proves a real
/// HTTP client can open a case, deduplicate a second attempt with the same
/// key onto the same row, transition its status with a note, and read back
/// the audit trail; also proves the two-tier permission split
/// (reports.view lets a view-only supervisor read, but only
/// reconciliation.manage can mutate).
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class ReconciliationCaseHttpTests : IAsyncLifetime
{
    private readonly ReconciliationCaseTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddReconciliationCaseExperience();

        _application = builder.Build();
        _application.MapReconciliationCaseApi();
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
        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PostAsJsonAsync(
            "/api/v1/management/reconciliation/cases",
            new CreateReconciliationCaseV1("RMD250-AUTH", CaseType.CashVariance, "A", "B", 10m, CaseSeverity.Low));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var denied = CreateClient(ReconciliationCaseTestDatabase.DeniedToken);
        using var forbidden = await denied.PostAsJsonAsync(
            "/api/v1/management/reconciliation/cases",
            new CreateReconciliationCaseV1("RMD250-AUTH", CaseType.CashVariance, "A", "B", 10m, CaseSeverity.Low));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AViewOnlySupervisorCanReadButNotCreateOrMutateACase()
    {
        using var supervisor = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);
        using var created = await supervisor.PostAsJsonAsync(
            "/api/v1/management/reconciliation/cases",
            new CreateReconciliationCaseV1("RMD250-VIEWONLY", CaseType.PaymentMismatch, "A", "B", 25m, CaseSeverity.Medium));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var opened = await created.Content.ReadFromJsonAsync<ReconciliationCaseV1>();

        using var viewOnly = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);
        using var read = await viewOnly.GetAsync($"/api/v1/management/reconciliation/cases/{opened!.CaseId:D}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var mutateAttempt = await viewOnly.PostAsJsonAsync(
            $"/api/v1/management/reconciliation/cases/{opened.CaseId:D}/notes",
            new AddReconciliationCaseNoteV1("should not be allowed"));
        Assert.Equal(HttpStatusCode.Forbidden, mutateAttempt.StatusCode);
    }

    [Fact]
    public async Task CreatingWithTheSameDeduplicationKeyReturnsTheSameCase()
    {
        using var client = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);
        var request = new CreateReconciliationCaseV1("RMD250-DEDUP", CaseType.InventoryDiscrepancy, "A", "B", 5m, CaseSeverity.Low);

        using var first = await client.PostAsJsonAsync("/api/v1/management/reconciliation/cases", request);
        var firstCase = await first.Content.ReadFromJsonAsync<ReconciliationCaseV1>();

        using var second = await client.PostAsJsonAsync("/api/v1/management/reconciliation/cases", request);
        var secondCase = await second.Content.ReadFromJsonAsync<ReconciliationCaseV1>();

        Assert.Equal(firstCase!.CaseId, secondCase!.CaseId);
    }

    [Fact]
    public async Task TransitioningAndAnnotatingACaseIsReflectedInItsAuditTrail()
    {
        using var client = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);
        using var created = await client.PostAsJsonAsync(
            "/api/v1/management/reconciliation/cases",
            new CreateReconciliationCaseV1("RMD250-TRANSITION", CaseType.FiscalDiscrepancy, "A", "B", 100m, CaseSeverity.High));
        var opened = await created.Content.ReadFromJsonAsync<ReconciliationCaseV1>();

        using var noted = await client.PostAsJsonAsync(
            $"/api/v1/management/reconciliation/cases/{opened!.CaseId:D}/notes",
            new AddReconciliationCaseNoteV1("Investigating the discrepancy."));
        Assert.Equal(HttpStatusCode.OK, noted.StatusCode);

        using var transitioned = await client.PostAsJsonAsync(
            $"/api/v1/management/reconciliation/cases/{opened.CaseId:D}/transition",
            new TransitionReconciliationCaseV1(CaseStatus.Investigating, opened.RowVersion));
        Assert.Equal(HttpStatusCode.OK, transitioned.StatusCode);
        var transitionedCase = await transitioned.Content.ReadFromJsonAsync<ReconciliationCaseV1>();
        Assert.Equal(CaseStatus.Investigating, transitionedCase!.Status);

        using var wrongVersion = await client.PostAsJsonAsync(
            $"/api/v1/management/reconciliation/cases/{opened.CaseId:D}/transition",
            new TransitionReconciliationCaseV1(CaseStatus.Resolved, opened.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, wrongVersion.StatusCode);

        using var actionsResponse = await client.GetAsync($"/api/v1/management/reconciliation/cases/{opened.CaseId:D}/actions");
        Assert.Equal(HttpStatusCode.OK, actionsResponse.StatusCode);
        var actions = await actionsResponse.Content.ReadFromJsonAsync<CaseActionV1[]>();
        Assert.Contains(actions!, a => a.ActionType == ActionType.NoteAdded);
        Assert.Contains(actions!, a => a.ActionType == ActionType.StatusChanged);
    }

    [Fact]
    public async Task ANonExistentCaseIsNotFound()
    {
        using var client = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);
        using var response = await client.GetAsync($"/api/v1/management/reconciliation/cases/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ReconciliationCaseEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("Reconciliation case PostgreSQL HTTP", DisableParallelization = true)]
public sealed class ReconciliationCasePostgresqlDefinition;
