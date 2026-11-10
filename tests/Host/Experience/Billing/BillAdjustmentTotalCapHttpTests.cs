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
        var cookies = new List<string>();
        for (var index = 0; index < 4; index++)
        {
            var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
            await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 5);
            cookies.Add(cookie);
        }

        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        // One cashier per comp, so the behavioural rate check of a single requester cannot be what sends the fourth to a manager.
        for (var index = 0; index < 3; index++)
        {
            using var within = await SendCompAsync(client, terminalId, seeded.BillId, cookies[index], 50m);
            Assert.Equal(HttpStatusCode.OK, within.StatusCode);
        }

        using var beyond = await SendCompAsync(client, terminalId, seeded.BillId, cookies[3], 50m);

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
    public async Task WhatASupervisorCompedDirectlyCountsTowardTheCapAndTheCashiersNextCompWaits()
    {
        var terminalId = Guid.NewGuid();
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.comp");
        var (userId, cashier) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 5);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var otherBill = await _database.SeedBillAsync();
        using var direct = await SendCompAsync(client, terminalId, seeded.BillId, supervisor, 120m);
        using var cashierComp = await SendCompAsync(client, terminalId, seeded.BillId, cashier, 50m);
        using var onAnotherBill = await SendCompAsync(client, terminalId, otherBill.BillId, cashier, 50m);

        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, cashierComp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, onAnotherBill.StatusCode);
    }

    [Fact]
    public async Task ADirectCompPastTheCapIsNotBlockedButTheRecordSaysTheManagerWentPastIt()
    {
        var terminalId = Guid.NewGuid();
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.comp");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var within = await SendCompAsync(client, terminalId, seeded.BillId, supervisor, 100m);
        using var past = await SendCompAsync(client, terminalId, seeded.BillId, supervisor, 100m);

        Assert.Equal(HttpStatusCode.OK, within.StatusCode);
        Assert.Equal(HttpStatusCode.OK, past.StatusCode);
        Assert.Equal([false, true], await CapExceededFlagsAsync(seeded.BillId));
        Assert.Equal(2L, await _database.BillAdjustmentCountAsync(seeded.BillId));
    }

    [Fact]
    public async Task AnUndoneCompNoLongerCountsAndTheCashierCanCompAgain()
    {
        var terminalId = Guid.NewGuid();
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.comp", "bills.split");
        var (userId, cashier) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 5);
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        using var direct = await SendCompAsync(client, terminalId, seeded.BillId, supervisor, 120m);

        using var blocked = await SendCompAsync(client, terminalId, seeded.BillId, cashier, 50m);
        var adjustmentId = await FirstAdjustmentIdAsync(seeded.BillId);
        using var undo = await SendUndoAsync(client, terminalId, seeded.BillId, adjustmentId, supervisor);
        using var again = await SendCompAsync(client, terminalId, seeded.BillId, cashier, 50m);

        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task ParallelCompsOfOneBillFromDifferentCashiersCannotPassTheCapTogether()
    {
        var terminalId = Guid.NewGuid();
        var cookies = new List<string>();
        for (var index = 0; index < 5; index++)
        {
            var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
            await _database.SetPolicyForRoleOfAsync(userId, "bills.comp", 50m, 8);
            cookies.Add(cookie);
        }

        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var responses = await Task.WhenAll(cookies.Select(cookie => SendCompAsync(client, terminalId, seeded.BillId, cookie, 50m)));

        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.Accepted));
        Assert.Equal(3L, await _database.BillAdjustmentCountAsync(seeded.BillId));
        foreach (var response in responses)
            response.Dispose();
    }

    [Fact]
    public async Task ADirectAdjustmentThatOnlyReachesTheCapIsNotMarkedButAPercentageThatPassesItIs()
    {
        var terminalId = Guid.NewGuid();
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(terminalId, "supervisor", "bills.comp", "bills.discount");
        var exact = await _database.SeedBillAsync();
        var passing = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var reaching = await SendCompAsync(client, terminalId, exact.BillId, supervisor, 150m);
        using var percentage = await SendDiscountAsync(client, terminalId, passing.BillId, supervisor, 70m);

        Assert.Equal(HttpStatusCode.OK, reaching.StatusCode);
        Assert.Equal(HttpStatusCode.OK, percentage.StatusCode);
        Assert.Equal([false], await CapExceededFlagsAsync(exact.BillId));
        Assert.Equal([true], await CapExceededFlagsAsync(passing.BillId, "bill.discount.applied"));
    }

    private async Task<List<bool>> CapExceededFlagsAsync(Guid billId, string eventName = "bill.comp.applied")
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var command = source.CreateCommand(
            "SELECT (after_state_json->>'totalCapExceeded')::boolean FROM audit.audit_events WHERE aggregate_id = @id AND event_name = @event ORDER BY occurred_at;");
        command.Parameters.AddWithValue("event", eventName);
        command.Parameters.AddWithValue("id", billId);
        var flags = new List<bool>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            flags.Add(reader.GetBoolean(0));
        return flags;
    }

    private async Task<Guid> FirstAdjustmentIdAsync(Guid billId)
    {
        await using var source = NpgsqlDataSource.Create(_database.ConnectionString);
        await using var command = source.CreateCommand(
            "SELECT bill_adjustment_id FROM billing.bill_adjustments WHERE bill_id = @id ORDER BY created_at LIMIT 1;");
        command.Parameters.AddWithValue("id", billId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<HttpResponseMessage> SendUndoAsync(
        HttpClient client, Guid terminalId, Guid billId, Guid adjustmentId, string cookie)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/adjustments/{adjustmentId:D}/undo");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
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
