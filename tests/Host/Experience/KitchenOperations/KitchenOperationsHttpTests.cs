using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.KitchenOperations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.KitchenOperations.Tests;

[Collection("Kitchen operations PostgreSQL HTTP")]
public sealed class KitchenOperationsHttpTests : IAsyncLifetime
{
    private readonly KitchenOperationsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task SessionAndCapabilityBoundariesReturn401And403()
    {
        var terminalId = Guid.NewGuid();
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, []);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var missing = await client.GetAsync(Prefix(terminalId) + "/tickets?stationId=hot-line");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var readableRequest = Request(
            HttpMethod.Get,
            Prefix(terminalId) + "/tickets?stationId=hot-line",
            readOnlyCookie);
        using var readable = await client.SendAsync(readableRequest);
        Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

        var seed = await _database.SeedKitchenGraphAsync();
        using var deniedRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            readOnlyCookie,
            new TransitionKitchenTicketV1("Accepted", 1));
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var error = await denied.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>();
        Assert.Equal("FORBIDDEN", error!.Error.Code);
        Assert.Equal(
            1L,
            await _database.ScalarAsync<long>(
                "SELECT count(*) FROM identity.denial_events WHERE permission_code = 'pos.cashier.mutate';"));
    }

    [Fact]
    public async Task TicketItemLifecycleUsesAuthoritativeVersionsAndRejectsStaleWrites()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [KitchenOperationsEndpoints.TicketMutationPermission]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var tickets = await GetAsync<KitchenTicketV1[]>(
            client, Prefix(terminalId) + "/tickets?stationId=hot-line", cookie);
        var ticket = Assert.Single(tickets!);
        Assert.Equal(seed.TicketId, ticket.Id);
        Assert.Equal("Queued", ticket.Status);
        var ticketJson = await GetStringAsync(
            client, Prefix(terminalId) + $"/tickets/{seed.TicketId:D}", cookie);
        Assert.DoesNotContain("internal note must not leave DTO", ticketJson, StringComparison.Ordinal);

        var accepted = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Accepted", 1));
        Assert.Equal(2, accepted.RowVersion);
        Assert.Equal(2, Assert.Single(accepted.Items).RowVersion);

        var preparing = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/transition",
            cookie,
            new TransitionKitchenItemV1("Preparing", 2, 2));
        Assert.Equal(3, preparing.RowVersion);
        Assert.Equal("Preparing", Assert.Single(preparing.Items).Status);

        using var staleRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Ready", 2));
        using var stale = await client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(
            "CONCURRENT_MODIFICATION",
            (await stale.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>())!.Error.Code);

        var readyItem = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/transition",
            cookie,
            new TransitionKitchenItemV1("Ready", 3, 3));
        Assert.Equal(4, readyItem.RowVersion);

        var readyTicket = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Ready", 4));
        Assert.Equal("Ready", readyTicket.Status);
    }

    [Fact]
    public async Task UnknownDeliveryRequiresReasonedReprintApprovalAndNeverAutoPrints()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [KitchenOperationsEndpoints.ReprintPermission]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var unknown = await GetAsync<PhysicalPrintDeliveryV1[]>(
            client, Prefix(terminalId) + "/deliveries/unknown", cookie);
        var delivery = Assert.Single(unknown!);
        Assert.Equal(seed.DeliveryId, delivery.Id);
        Assert.Equal("Unknown", delivery.Status);
        var serialized = await GetStringAsync(
            client, Prefix(terminalId) + $"/deliveries/{seed.DeliveryId:D}", cookie);
        Assert.DoesNotContain("secret-payload", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadSnapshot", serialized, StringComparison.Ordinal);

        using var missingReason = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/deliveries/{seed.DeliveryId:D}/reprint-approval",
            cookie,
            new ReprintDecisionV1(""));
        using var invalid = await client.SendAsync(missingReason);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var approved = await PostAsync<PhysicalPrintDeliveryV1>(
            client,
            $"{Prefix(terminalId)}/deliveries/{seed.DeliveryId:D}/reprint-approval",
            cookie,
            new ReprintDecisionV1("Station checked; original ticket was not printed."));
        Assert.Equal("ReprintApproved", approved.Status);
        Assert.True(approved.IsReprint);
        Assert.Equal(
            "ReprintApproved",
            await _database.ScalarAsync<string>(
                "SELECT status FROM kitchen.physical_print_deliveries WHERE id = '" + seed.DeliveryId + "';"));
    }

    [Fact]
    public async Task HealthBackupPrinterRouteAndAuditSurfacesAreMinimizedAndFailClosed()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [KitchenOperationsEndpoints.RoutingMutationPermission, KitchenOperationsEndpoints.BackupPermission]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var health = await GetAsync<HealthSnapshotV1>(
            client, Prefix(terminalId) + "/operations/health/latest", cookie);
        Assert.Equal("Unhealthy", health!.DiskStatus);
        Assert.Equal("Unhealthy", health.LastBackupStatus);

        var backups = await GetAsync<BackupV1[]>(
            client, Prefix(terminalId) + "/operations/backups/recent", cookie);
        var backup = Assert.Single(backups!);
        Assert.Equal("Failed", backup.Status);
        var backupJson = await GetStringAsync(
            client, Prefix(terminalId) + "/operations/backups/recent", cookie);
        Assert.DoesNotContain("FilePath", backupJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ChecksumSha256", backupJson, StringComparison.Ordinal);

        using var backupRequest = JsonRequest(
            HttpMethod.Post,
            Prefix(terminalId) + "/operations/backups",
            cookie,
            new StartBackupV1("Full"));
        using var backupResponse = await client.SendAsync(backupRequest);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, backupResponse.StatusCode);
        Assert.Equal(
            "BACKUP_NOT_CONFIGURED",
            (await backupResponse.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>())!.Error.Code);

        var printers = await GetAsync<PrinterV1[]>(
            client, Prefix(terminalId) + "/printers", cookie);
        Assert.Contains(printers!, printer => printer.Id == seed.PrinterId);
        var routes = await GetAsync<PrinterRouteV1[]>(
            client, Prefix(terminalId) + "/routes", cookie);
        Assert.Contains(routes!, route => route.Id == seed.RouteId);

        var audit = await GetAsync<AuditEventV1[]>(
            client, Prefix(terminalId) + $"/audit/aggregate/KitchenTicket/{seed.TicketId:D}", cookie);
        var auditEvent = Assert.Single(audit!);
        Assert.Equal("KitchenTicketTransitioned", auditEvent.EventName);
        var auditJson = await GetStringAsync(
            client, Prefix(terminalId) + $"/audit/aggregate/KitchenTicket/{seed.TicketId:D}", cookie);
        Assert.DoesNotContain("beforeStateJson", auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", auditJson, StringComparison.Ordinal);
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddKitchenOperationsExperience();
        var app = builder.Build();
        app.MapKitchenOperationsApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<TResponse> PostAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie,
        object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"POST {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<TResponse> GetAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie)
    {
        using var request = Request(HttpMethod.Get, path, cookie);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"GET {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<string> GetStringAsync(
        HttpClient client,
        string path,
        string cookie)
    {
        using var request = Request(HttpMethod.Get, path, cookie);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"GET {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadAsStringAsync();
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(
        HttpMethod method,
        string path,
        string cookie,
        object body)
    {
        var request = Request(method, path, cookie);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static string Prefix(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/kitchen-operations";
}

[CollectionDefinition("Kitchen operations PostgreSQL HTTP", DisableParallelization = true)]
public sealed class KitchenOperationsPostgresqlDefinition;
