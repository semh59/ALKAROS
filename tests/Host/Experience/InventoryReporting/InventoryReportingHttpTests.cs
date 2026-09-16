using System.Net;
using System.Net.Http.Json;
using ALKAROS.Reporting.MenuInventory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.InventoryReporting.Tests;

/// <summary>
/// V11-RPT-002: <c>GetCriticalStockReportAsync</c> had no Host endpoint at
/// all before this — these prove a real HTTP client can read it, and that
/// the reports.view gate is actually enforced.
/// </summary>
[Collection("Inventory reporting PostgreSQL HTTP")]
public sealed class InventoryReportingHttpTests : IAsyncLifetime
{
    private readonly InventoryReportingTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddInventoryReportingExperience();

        _application = builder.Build();
        _application.MapInventoryReportingApi();
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
    public async Task MissingAndUnpermissionedSessionsAreRejected()
    {
        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.GetAsync("/api/v1/management/inventory/reports/critical-stock");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var denied = CreateClient(InventoryReportingTestDatabase.DeniedToken);
        using var forbidden = await denied.GetAsync("/api/v1/management/inventory/reports/critical-stock");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ReturnsCriticalStockReportUsingThePersistedReorderPoint()
    {
        var locationId = await _database.SeedStockLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        var criticalItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8], reorderPoint: 20m);
        var normalItemId = await _database.SeedStockItemAsync("ITEM-" + Guid.NewGuid().ToString("N")[..8], reorderPoint: 20m);
        await _database.SeedStockBalanceAsync(criticalItemId, locationId, 5m);
        await _database.SeedStockBalanceAsync(normalItemId, locationId, 50m);

        using var client = CreateClient(InventoryReportingTestDatabase.ViewerToken);
        using var response = await client.GetAsync($"/api/v1/management/inventory/reports/critical-stock?locationId={locationId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<CriticalStockReport>();
        Assert.Contains(report!.Items, i => i.StockItemId == criticalItemId && i.IsCritical);
        Assert.Contains(report.Items, i => i.StockItemId == normalItemId && !i.IsCritical);
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{InventoryReportingEndpoints.ManagerCookieName}={token}");
        return client;
    }
}

[CollectionDefinition("Inventory reporting PostgreSQL HTTP", DisableParallelization = true)]
public sealed class InventoryReportingPostgresqlDefinition;
