using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reporting.Tests;

/// <summary>
/// V1-RMD-249: found by an independent audit (2026-09-18) —
/// IOperationalReportService (V1-RPT-001) had zero HTTP surface. Proves a
/// real HTTP client can open a real business day, close it with waiter/print
/// summaries in one call, and read back the combined full-day report; also
/// proves the two-tier permission split (reports.view lets a supervisor
/// read, but only reports.close-day/manager can open or close a day).
/// </summary>
[Collection("EOD business day PostgreSQL HTTP")]
public sealed class EndOfDayHttpTests : IAsyncLifetime
{
    private readonly EndOfDayTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddEndOfDayExperience();

        _application = builder.Build();
        _application.MapEndOfDayApi();
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
        var businessDate = new DateOnly(2026, 1, 1);

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.GetAsync($"/api/v1/management/reporting/business-day/{businessDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var denied = CreateClient(EndOfDayTestDatabase.DeniedToken);
        using var forbidden = await denied.GetAsync($"/api/v1/management/reporting/business-day/{businessDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ASupervisorCanReadButNotOpenOrCloseADay()
    {
        var businessDate = new DateOnly(2026, 1, 2);
        using var manager = CreateClient(EndOfDayTestDatabase.ManagerToken);
        using var opened = await manager.PostAsJsonAsync(
            "/api/v1/management/reporting/business-day/open", new OpenBusinessDayV1(businessDate));
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);

        using var supervisor = CreateClient(EndOfDayTestDatabase.SupervisorToken);
        using var read = await supervisor.GetAsync($"/api/v1/management/reporting/business-day/{businessDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var supervisorOpenAttempt = await supervisor.PostAsJsonAsync(
            "/api/v1/management/reporting/business-day/open", new OpenBusinessDayV1(businessDate.AddDays(1)));
        Assert.Equal(HttpStatusCode.Forbidden, supervisorOpenAttempt.StatusCode);

        using var supervisorCloseAttempt = await supervisor.PostAsJsonAsync(
            $"/api/v1/management/reporting/business-day/{businessDate:yyyy-MM-dd}/close",
            new CloseBusinessDayV1(1000m, 10, 1, 0));
        Assert.Equal(HttpStatusCode.Forbidden, supervisorCloseAttempt.StatusCode);
    }

    [Fact]
    public async Task AManagerCanOpenCloseAndReadTheFullReport()
    {
        var businessDate = new DateOnly(2026, 1, 3);
        using var client = CreateClient(EndOfDayTestDatabase.ManagerToken);

        using var opened = await client.PostAsJsonAsync(
            "/api/v1/management/reporting/business-day/open", new OpenBusinessDayV1(businessDate));
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var openedDay = await opened.Content.ReadFromJsonAsync<BusinessDayV1>();
        Assert.Equal("Open", openedDay!.Status);

        var waiterId = Guid.NewGuid();
        using var closed = await client.PostAsJsonAsync(
            $"/api/v1/management/reporting/business-day/{businessDate:yyyy-MM-dd}/close",
            new CloseBusinessDayV1(
                TotalRevenue: 5000.50m,
                TotalOrders: 42,
                CancelledItems: 2,
                PrintFailures: 1,
                WaiterSummaries: [new WaiterPerformanceV1(waiterId, 10, 1200m, 1, 50m)],
                PrintSummaries: [new PrintErrorSummaryV1("Kitchen-1", 20, 1, 1)]));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var report = await closed.Content.ReadFromJsonAsync<BusinessDayReportV1>();
        Assert.Equal("Closed", report!.BusinessDay.Status);
        Assert.Equal(5000.50m, report.BusinessDay.TotalRevenue);
        Assert.Single(report.WaiterSummaries);
        Assert.Single(report.PrintSummaries);

        using var full = await client.GetAsync($"/api/v1/management/reporting/business-day/{businessDate:yyyy-MM-dd}/full-report");
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        var fullReport = await full.Content.ReadFromJsonAsync<BusinessDayReportV1>();
        Assert.Equal(report.BusinessDay.BusinessDayId, fullReport!.BusinessDay.BusinessDayId);
    }

    [Fact]
    public async Task OpeningTheSameDayTwiceIsRejected()
    {
        var businessDate = new DateOnly(2026, 1, 4);
        using var client = CreateClient(EndOfDayTestDatabase.ManagerToken);

        using var first = await client.PostAsJsonAsync(
            "/api/v1/management/reporting/business-day/open", new OpenBusinessDayV1(businessDate));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync(
            "/api/v1/management/reporting/business-day/open", new OpenBusinessDayV1(businessDate));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ANonExistentDayIsNotFound()
    {
        using var client = CreateClient(EndOfDayTestDatabase.ManagerToken);
        using var response = await client.GetAsync("/api/v1/management/reporting/business-day/2026-12-31");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{EndOfDayEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("EOD business day PostgreSQL HTTP", DisableParallelization = true)]
public sealed class EndOfDayPostgresqlDefinition;
