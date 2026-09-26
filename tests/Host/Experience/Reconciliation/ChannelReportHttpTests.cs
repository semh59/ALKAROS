using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.Experience.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// V12-RPT-001 over HTTP with the real module composition: the versioned channel report is read with the
/// manager session plus reports.view, runs against the real schema and refuses an invalid range with the
/// shared Turkish validation error.
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class ChannelReportHttpTests : IAsyncLifetime
{
    private const string ReportPath = "/api/v1/management/reports/channels?from=2026-09-20&to=2026-09-26";

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
        _application.MapChannelReportApi();
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
    public async Task AViewOnlySupervisorReadsTheVersionedReportAndOthersCannot()
    {
        using var anonymous = CreateClient(null);
        using var denied = CreateClient(ReconciliationCaseTestDatabase.DeniedToken);
        using var viewOnly = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);
        await _database.ExecuteAsync(
            """
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, total, created_at, updated_at)
            VALUES (gen_random_uuid(), 'Qr', 'Served', 'Accepted', 'QR-HTTP-1', 42.50, '2026-09-24T10:00:00Z', '2026-09-24T10:00:00Z');
            """);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ReportPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(ReportPath)).StatusCode);

        using var response = await viewOnly.GetAsync(ReportPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("channel-report.v1", body.GetProperty("reportVersion").GetString());
        var day = Assert.Single(body.GetProperty("days").EnumerateArray());
        Assert.Equal("2026-09-24", day.GetProperty("businessDate").GetString());
        Assert.Equal(42.50m, day.GetProperty("acceptedValue").GetDecimal());
        Assert.True(body.GetProperty("check").GetProperty("isBalanced").GetBoolean());
    }

    [Fact]
    public async Task AReversedRangeIsRefusedInTurkish()
    {
        using var supervisor = CreateClient(ReconciliationCaseTestDatabase.SupervisorToken);

        using var response = await supervisor.GetAsync("/api/v1/management/reports/channels?from=2026-09-26&to=2026-09-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("VALIDATION_FAILED", error.GetProperty("code").GetString());
        Assert.Equal("İstek doğrulanamadı.", error.GetProperty("message").GetString());
    }

    private HttpClient CreateClient(string? managerToken)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (managerToken is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ReconciliationCaseEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }
}
