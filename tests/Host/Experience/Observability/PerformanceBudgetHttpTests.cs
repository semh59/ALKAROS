using System.Net;
using System.Net.Http.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Observability.PerformanceBudgetEndpoints;
using ALKAROS.Host.Observability.Wiring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Observability.Tests;

[Collection("Observability PostgreSQL HTTP")]
public sealed class PerformanceBudgetHttpTests : IAsyncLifetime
{
    private const string Path = "/api/v1/management/observability/performance-budgets";
    private readonly ObservabilityTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddSingleton(new MonitoringDataSource(_database.DataSource));
        builder.Services.AddSingleton<IAuditEventStore>(new PostgresAuditEventStore(_database.DataSource));
        builder.Services.AddObservabilityExperience();
        builder.Services.AddPerformanceBudgetEndpoints(800);
        _application = builder.Build();
        _application.MapPerformanceBudgetEndpoints();
        await _application.StartAsync();
        _baseAddress = new Uri(Assert.Single(_application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses));
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    private HttpClient Client(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ObservabilityEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<PerformanceBudgetList> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<PerformanceBudgetList>(Path))!;

    [Fact]
    public async Task ManagerSetsReadsOverwritesAndRemovesABudgetAndEveryChangeIsAudited()
    {
        using var manager = Client(ObservabilityTestDatabase.SupervisorToken);

        using var created = await manager.PutAsJsonAsync(Path, new PerformanceBudgetItem("request", "GET /api/x", 450));
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        using var changed = await manager.PutAsJsonAsync(Path, new PerformanceBudgetItem("request", "GET /api/x", 300));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var list = await ListAsync(manager);
        Assert.Equal(800, list.DefaultRequestP95Ms);
        Assert.Equal(300, Assert.Single(list.Items, i => i.Name == "GET /api/x").P95Ms);

        using var removed = await manager.DeleteAsync($"{Path}?metric=request&name={Uri.EscapeDataString("GET /api/x")}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var again = await manager.DeleteAsync($"{Path}?metric=request&name={Uri.EscapeDataString("GET /api/x")}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.DoesNotContain((await ListAsync(manager)).Items, i => i.Name == "GET /api/x");

        Assert.Equal(2, await _database.ScalarAsync<long>(
            "SELECT count(*) FROM audit.audit_events WHERE event_name = 'monitoring.performance_budget.set';"));
        Assert.Equal(1, await _database.ScalarAsync<long>(
            "SELECT count(*) FROM audit.audit_events WHERE event_name = 'monitoring.performance_budget.removed';"));
    }

    [Fact]
    public async Task ViewOnlyUserReadsButChangesAreForbiddenAndLeaveNoTrace()
    {
        using var viewOnly = Client(ObservabilityTestDatabase.ViewOnlyToken);
        using var anonymous = Client(null);

        Assert.Equal(HttpStatusCode.OK, (await viewOnly.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.PutAsJsonAsync(Path, new PerformanceBudgetItem("request", "GET /y", 100))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewOnly.DeleteAsync($"{Path}?metric=request&name=x")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(Path, new PerformanceBudgetItem("request", "GET /y", 100))).StatusCode);

        Assert.Equal(0, await _database.ScalarAsync<long>("SELECT count(*) FROM observability.perf_budgets;"));
        Assert.Equal(0, await _database.ScalarAsync<long>(
            "SELECT count(*) FROM audit.audit_events WHERE event_name LIKE 'monitoring.performance_budget.%';"));
    }

    [Theory]
    [InlineData("nope", "GET /x", 100)]
    [InlineData("request", "", 100)]
    [InlineData("request", "GET /x", 0)]
    [InlineData("request", "GET /x", -5)]
    [InlineData("request", "GET /x", 3_600_001)]
    public async Task InvalidBudgetsAreRejectedAndNotStored(string metric, string name, double p95)
    {
        using var manager = Client(ObservabilityTestDatabase.SupervisorToken);

        using var response = await manager.PutAsJsonAsync(Path, new PerformanceBudgetItem(metric, name, p95));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _database.ScalarAsync<long>("SELECT count(*) FROM observability.perf_budgets;"));
    }

    [Fact]
    public async Task OverlongNameIsRejected()
    {
        using var manager = Client(ObservabilityTestDatabase.SupervisorToken);

        using var response = await manager.PutAsJsonAsync(Path, new PerformanceBudgetItem("request", new string('a', 201), 100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
