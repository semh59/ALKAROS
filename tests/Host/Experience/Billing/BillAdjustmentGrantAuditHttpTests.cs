using System.Net;
using System.Net.Http.Json;
using ALKAROS.Audit.EventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>
/// A comp or discount that waits for a manager or is refused leaves an audit trail too; the cashier screen polls a
/// waiting request by resending it, and every poll must find the same single record.
/// </summary>
[Collection("Billing split PostgreSQL HTTP")]
public sealed class BillAdjustmentGrantAuditHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Theory]
    [InlineData("comp")]
    [InlineData("discount")]
    public async Task AWaitingRequestWritesOnePendingRecordHoweverOftenItIsPolled(string kind)
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString();

        for (var poll = 0; poll < 5; poll++)
        {
            using var response = await SendAsync(client, terminalId, seeded.BillId, cookie, kind, key);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var pending = Assert.Single(await EventsAsync(seeded.BillId));
        Assert.Equal($"bill.{kind}.pending", pending.EventName);
        Assert.Equal(userId, pending.ActorId);
        Assert.Equal(seeded.BillId, pending.AggregateId);
        Assert.Equal(kind == "comp" ? "CustomerSatisfaction" : "PromotionalOffer", pending.Reason);
        using var after = System.Text.Json.JsonDocument.Parse(pending.AfterStateJson!);
        Assert.Equal(10m, after.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("FixedAmount", after.RootElement.GetProperty("calculationType").GetString());
        Assert.True(after.RootElement.TryGetProperty("grantId", out _));
    }

    [Theory]
    [InlineData("comp")]
    [InlineData("discount")]
    public async Task ARefusedRequestWritesOneDeniedRecordAfterTheWaitingOne(string kind)
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString();

        using var waiting = await SendAsync(client, terminalId, seeded.BillId, cookie, kind, key);
        await _database.DenyGrantAsync((await waiting.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>())!.GrantId!.Value);
        for (var poll = 0; poll < 3; poll++)
        {
            using var refused = await SendAsync(client, terminalId, seeded.BillId, cookie, kind, key);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var names = (await EventsAsync(seeded.BillId)).Select(e => e.EventName).Order().ToArray();
        Assert.Equal([$"bill.{kind}.denied", $"bill.{kind}.pending"], names);
    }

    [Fact]
    public async Task ARequestRefusedAtOnceWritesADeniedRecordWithoutAWaitingOne()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        await using (var policies = NpgsqlDataSource.Create(_database.ConnectionString))
        await using (var deny = policies.CreateCommand(
            """
            INSERT INTO identity.authorization_policies (permission_code, role_code, mode)
            SELECT 'bills.comp', r.code, 'always_deny'
            FROM identity.user_roles ur JOIN identity.roles r ON r.role_id = ur.role_id WHERE ur.user_id = @user_id;
            """))
        {
            deny.Parameters.AddWithValue("user_id", userId);
            await deny.ExecuteNonQueryAsync();
        }
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var refused = await SendAsync(client, terminalId, seeded.BillId, cookie, "comp", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var denied = Assert.Single(await EventsAsync(seeded.BillId));
        Assert.Equal("bill.comp.denied", denied.EventName);
        Assert.Equal(userId, denied.ActorId);
        Assert.Equal("CustomerSatisfaction", denied.Reason);
    }

    [Fact]
    public async Task ConcurrentPollsOfAWaitingRequestWriteOnePendingRecord()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString();
        using var first = await SendAsync(client, terminalId, seeded.BillId, cookie, "comp", key);

        var polls = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendAsync(client, terminalId, seeded.BillId, cookie, "comp", key)));

        Assert.All(polls, poll => Assert.Equal(HttpStatusCode.Accepted, poll.StatusCode));
        Assert.Equal("bill.comp.pending", Assert.Single(await EventsAsync(seeded.BillId)).EventName);
    }

    [Fact]
    public async Task AnApprovedRequestKeepsItsWaitingRecordAndAddsTheAppliedOne()
    {
        var terminalId = Guid.NewGuid();
        var (_, cookie) = await _database.SeedSessionWithPermissionsAsync(terminalId, "cashier");
        var seeded = await _database.SeedBillAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var key = Guid.NewGuid().ToString();

        using var waiting = await SendAsync(client, terminalId, seeded.BillId, cookie, "comp", key);
        await _database.ApproveGrantAsync((await waiting.Content.ReadFromJsonAsync<ApplyBillDiscountResultV1>())!.GrantId!.Value);
        using var applied = await SendAsync(client, terminalId, seeded.BillId, cookie, "comp", key);

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var names = (await EventsAsync(seeded.BillId)).Select(e => e.EventName).Order().ToArray();
        Assert.Equal(["bill.comp.applied", "bill.comp.pending"], names);
    }

    private async Task<IReadOnlyList<AuditEvent>> EventsAsync(Guid billId)
    {
        await using var dataSource = NpgsqlDataSource.Create(_database.ConnectionString);
        return await new PostgresAuditEventStore(dataSource).GetByAggregateAsync("Bill", billId);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, Guid terminalId, Guid billId, string cookie, string kind, string key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/{kind}")
        {
            Content = kind == "comp"
                ? JsonContent.Create(new ApplyBillCompRequestV1(key, 10m, "CustomerSatisfaction"))
                : JsonContent.Create(new ApplyBillDiscountRequestV1(key, "FixedAmount", 10m, "PromotionalOffer")),
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
