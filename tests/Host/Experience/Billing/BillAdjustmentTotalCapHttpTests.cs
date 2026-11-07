using System.Net;
using System.Net.Http.Json;
using ALKAROS.Settings.BillAdjustmentCap;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>
/// The per-bill comp and discount cap over the real billing endpoints and a real PostgreSQL: a cashier whose single
/// comp fits the role limit still waits for a manager once the bill's grants add up past the cap, and the cap follows
/// the setting.
/// </summary>
[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillAdjustmentTotalCapHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task CashierCompsOfOneBillAddUpToTheDefaultCapAndTheNextOneWaitsForAManager()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 5);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        for (var index = 0; index < 3; index++)
        {
            using var within = await SendCompAsync(client, terminalId, seeded.BillId, cookie, 50m);
            Assert.Equal(HttpStatusCode.OK, within.StatusCode);
        }

        using var beyond = await SendCompAsync(client, terminalId, seeded.BillId, cookie, 50m);

        Assert.Equal(HttpStatusCode.Accepted, beyond.StatusCode);
        Assert.Equal("Pending", (await beyond.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>())!.Status);
        Assert.Equal(3L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task TheCapFollowsTheSetting()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 5);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var settings = app.Services.GetRequiredService<ISettingsService>();
        await BillAdjustmentTotalCapSetting.EnsureRegisteredAsync(settings, SettingsBillAdjustmentTotalCap.DefaultCap);
        var record = (await settings.GetRecordAsync(BillAdjustmentTotalCapSetting.Key))!;
        await settings.SetValueAsync(BillAdjustmentTotalCapSetting.Key, 60, record.RowVersion);

        using var first = await SendCompAsync(client, terminalId, seeded.BillId, cookie, 50m);
        using var second = await SendCompAsync(client, terminalId, seeded.BillId, cookie, 20m);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
    }

    [Fact]
    public async Task PercentageDiscountsOfOneBillAddUpToTheCapToo()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.discount", 120m, 5);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var first = await SendDiscountAsync(client, terminalId, seeded.BillId, cookie, 40m);
        using var second = await SendDiscountAsync(client, terminalId, seeded.BillId, cookie, 40m);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
    }

    [Fact]
    public async Task ARoleHoldingCompOutrightIsNotCapped()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.comp");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var large = await SendCompAsync(client, terminalId, seeded.BillId, cookie, 200m);

        Assert.Equal(HttpStatusCode.OK, large.StatusCode);
    }

    [Fact]
    public void TheDefaultCapIsThePersonalCompBudgetDailyCeiling()
        => Assert.Equal((int)ALKAROS.Identity.Authorization.PersonalBudgets.PersonalCompBudgetPolicy.DailyCap, SettingsBillAdjustmentTotalCap.DefaultCap);

    private static async Task<HttpResponseMessage> SendCompAsync(
        HttpClient client, Guid terminalId, Guid billId, string cookie, decimal amount)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/comp")
        {
            Content = JsonContent.Create(new ApplyBillCompRequestV1(Guid.NewGuid().ToString(), amount, "CustomerSatisfaction")),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendDiscountAsync(
        HttpClient client, Guid terminalId, Guid billId, string cookie, decimal percent)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/discount")
        {
            Content = JsonContent.Create(new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "Percentage", percent, "PromotionalOffer")),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return client;
    }
}
