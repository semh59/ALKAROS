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
/// V1-RMD-265: the payment settlement report (V13-RPT-001) and the payment
/// discrepancy scan (V13-REC-001) were built and DI-registered but no caller
/// existed in the running host. Proves both are now reachable over HTTP with
/// the real module composition, behind the manager session: reports.view reads
/// the report, and only reconciliation.manage may run the scan (it writes cases).
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class PaymentSettlementHttpTests : IAsyncLifetime
{
    private const string ReportPath = "/api/v1/management/payments/settlement-report?businessDate=2026-09-24";
    private const string ScanPath = "/api/v1/management/payments/reconciliation-scan";

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

        _application = builder.Build();
        _application.MapPaymentSettlementApi();
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
    public async Task ReportAndScanRejectAnonymousAndUnpermissionedSessions()
    {
        using var anonymous = CreateClient(null);
        using var denied = CreateClient(ReconciliationCaseTestDatabase.DeniedToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ReportPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(ScanPath, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(ReportPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsync(ScanPath, null)).StatusCode);
    }

    [Fact]
    public async Task AViewOnlySupervisorReadsTheReportWithHonestlyDisabledSectionsButCannotRunTheScan()
    {
        using var viewOnly = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);

        using var report = await viewOnly.GetAsync(ReportPath);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var body = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-09-24", body.GetProperty("businessDate").GetString());
        // Sections whose source data does not exist yet say so, never a silent zero.
        Assert.Equal("V13-ALC-004", body.GetProperty("netRefunds").GetProperty("blockedBy").GetString());
        Assert.Equal("V13-FSC-001", body.GetProperty("fiscalStatus").GetProperty("blockedBy").GetString());
        Assert.Equal("V13-MCD-002", body.GetProperty("mealCardClosure").GetProperty("blockedBy").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PostAsync(ScanPath, null)).StatusCode);
    }

    [Fact]
    public async Task ASupervisorWithManageRunsTheScanAndEveryDisabledSourceReportsItself()
    {
        using var supervisor = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);

        using var scan = await supervisor.PostAsync(ScanPath, null);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var results = await scan.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, results.GetArrayLength());
        var disabled = results.EnumerateArray().Where(r => !r.GetProperty("wasEnabled").GetBoolean()).ToList();
        Assert.Equal(3, disabled.Count);
        Assert.All(disabled, r => Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("disabledReason").GetString())));
        Assert.All(
            results.EnumerateArray().Where(r => r.GetProperty("wasEnabled").GetBoolean()),
            r => Assert.Equal(JsonValueKind.Null, r.GetProperty("failureReason").ValueKind));
    }

    private HttpClient CreateClient(string? managerToken)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (managerToken is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ReconciliationCaseEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }
}
