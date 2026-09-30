using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.InvoiceSettings;
using ALKAROS.Invoicing.Generation.OrderInvoices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.QnbCredentialSettings.Tests;

/// <summary>
/// The seller profile endpoints over real HTTP and Postgres: manager session only (integrations.manage), a complete
/// profile is kept and read back, an incomplete one is refused with the Turkish names of the fields to fix.
/// </summary>
[Collection("QNB credential settings PostgreSQL HTTP")]
public sealed class SellerProfileSettingsHttpTests : IAsyncLifetime
{
    private readonly QnbCredentialSettingsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SellerProfile Complete() => new(
        "Deniz Lokantası Ltd. Şti.", SellerProfile.Vkn, "1234567890", "Kadıköy", "Moda Cad. 1", "Kadıköy", "İstanbul", "muhasebe@deniz.example");

    private static string Path(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/invoice-settings/seller-profile";

    [Fact]
    public async Task NoSessionIsUnauthorizedAndACashierWithoutIntegrationsManageIsForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "cashier", "orders.create");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var anonymous = await client.SendAsync(Request(HttpMethod.Get, Path(terminalId), null));
        using var forbidden = await client.SendAsync(Request(HttpMethod.Put, Path(terminalId), cashier, Complete()));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AManagerSavesAProfileAndReadsItBackAndALaterSaveReplacesIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var before = await client.SendAsync(Request(HttpMethod.Get, Path(terminalId), cookie));
        var empty = await before.Content.ReadFromJsonAsync<SellerProfileResponse>();
        Assert.False(empty!.Configured);
        Assert.Null(empty.Profile);

        using var saved = await client.SendAsync(Request(HttpMethod.Put, Path(terminalId), cookie, Complete()));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using var replaced = await client.SendAsync(Request(HttpMethod.Put, Path(terminalId), cookie, Complete() with { LegalName = "Yeni Ünvan A.Ş.", Email = null }));
        Assert.Equal(HttpStatusCode.NoContent, replaced.StatusCode);

        using var after = await client.SendAsync(Request(HttpMethod.Get, Path(terminalId), cookie));
        var read = await after.Content.ReadFromJsonAsync<SellerProfileResponse>();
        Assert.True(read!.Configured);
        Assert.Equal("Yeni Ünvan A.Ş.", read.Profile!.LegalName);
        Assert.Null(read.Profile.Email);
    }

    [Fact]
    public async Task AnIncompleteProfileIsRefusedWithTheTurkishFieldNamesAndNothingIsStored()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(terminalId, "manager", "integrations.manage");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Request(
            HttpMethod.Put, Path(terminalId), cookie, Complete() with { TaxIdNumber = "123", TaxOffice = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = body.RootElement.GetProperty("error");
        Assert.Equal("VALIDATION_FAILED", error.GetProperty("code").GetString());
        Assert.Equal("Şu alanlar eksik ya da hatalı: Vergi dairesi, Vergi veya TC kimlik numarası.", error.GetProperty("message").GetString());
        Assert.Equal(["taxOffice", "taxIdNumber"], error.GetProperty("fields").EnumerateArray().Select(field => field.GetString()));

        using var after = await client.SendAsync(Request(HttpMethod.Get, Path(terminalId), cookie));
        Assert.False((await after.Content.ReadFromJsonAsync<SellerProfileResponse>())!.Configured);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string? cookie, object? body = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddInvoiceSettingsExperience();
        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy("terminal-read", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("r"));
            limiter.AddPolicy("terminal-write", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("w"));
        });

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapInvoiceSettingsApi();
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
