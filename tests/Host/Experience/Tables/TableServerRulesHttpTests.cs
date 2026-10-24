using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Tables;
using ALKAROS.Tables.Reservations;
using ALKAROS.Tables.TableMerge;
using ALKAROS.Tables.TableTransfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Tables;

[Collection("Table management PostgreSQL HTTP")]
public sealed class TableServerRulesHttpTests : IAsyncLifetime
{
    private readonly TableManagementTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ReservationAndMergeTimesComeFromTheServerNotTheClient()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var stale = new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var zone = await PostAsync<ZoneDto>(client, Prefix(terminalId) + "/zones", cookie, new CreateZoneRequest(0, "ZSR1", "Hall SR1"));
        var reserved = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-01");

        var reservation = await PostAsync<TableReservationResult>(client, Prefix(terminalId) + "/reservations", cookie,
            new CreateTableReservationRequest(reserved.TableId, reserved.RowVersion, null, "Phone", 2, stale));
        Assert.True(await _database.ScalarAsync<bool>(
            $"SELECT reserved_at > now() - interval '1 minute' FROM table_mgmt.table_reservations WHERE table_reservation_id = '{reservation.ReservationId:D}';"));

        var primary = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-02");
        var joined = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-03");
        await PostAsync<object>(client, Prefix(terminalId) + "/merges", cookie,
            new MergeTablesRequest(primary.TableId, primary.RowVersion, [new TableMergeParticipantRequest(joined.TableId, joined.RowVersion)], "Group", stale));
        Assert.True(await _database.ScalarAsync<bool>(
            $"SELECT merged_at > now() - interval '1 minute' FROM table_mgmt.table_merges WHERE primary_table_id = '{primary.TableId:D}';"));
    }

    [Fact]
    public async Task EveryOtherOperationTimeAlsoComesFromTheServer()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var stale = new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var zone = await PostAsync<ZoneDto>(client, Prefix(terminalId) + "/zones", cookie, new CreateZoneRequest(0, "ZSR5", "Hall SR5"));
        var reservations = Prefix(terminalId) + "/reservations";

        var claimTable = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-41");
        var toClaim = await PostAsync<TableReservationResult>(client, reservations, cookie,
            new CreateTableReservationRequest(claimTable.TableId, claimTable.RowVersion, null, "Claim"));
        var claimed = await PostAsync<TableReservationReleaseResult>(client, $"{reservations}/{toClaim.ReservationId:D}/claim", cookie,
            new ClaimTableReservationRequest(toClaim.ReservationRowVersion, toClaim.NewTableRowVersion, null, stale));
        AssertRecent(claimed.ReleasedAt);

        var cancelTable = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-42");
        var toCancel = await PostAsync<TableReservationResult>(client, reservations, cookie,
            new CreateTableReservationRequest(cancelTable.TableId, cancelTable.RowVersion, null, "Cancel"));
        var cancelled = await PostAsync<TableReservationReleaseResult>(client, $"{reservations}/{toCancel.ReservationId:D}/cancel", cookie,
            new CancelTableReservationRequest(toCancel.ReservationRowVersion, toCancel.NewTableRowVersion, "No show", stale));
        AssertRecent(cancelled.ReleasedAt);

        var expireTable = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-43");
        var toExpire = await PostAsync<TableReservationResult>(client, reservations, cookie,
            new CreateTableReservationRequest(expireTable.TableId, expireTable.RowVersion, null, "Expire", 1, null, DateTimeOffset.UtcNow.AddHours(1)));
        await Exec("UPDATE table_mgmt.table_reservations SET reserved_at = now() - interval '2 hours', expires_at = now() - interval '1 hour' WHERE table_reservation_id = @id;",
            ("id", toExpire.ReservationId));
        var expired = await PostAsync<TableReservationReleaseResult>(client, $"{reservations}/{toExpire.ReservationId:D}/expire", cookie,
            new ExpireTableReservationRequest(toExpire.ReservationRowVersion, toExpire.NewTableRowVersion, "Due", stale));
        AssertRecent(expired.ReleasedAt);

        var source = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-44");
        var target = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-45");
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", source.TableId));
        var transfer = await PostAsync<TableTransferResult>(client, Prefix(terminalId) + "/transfers", cookie,
            new TransferTableRequest(source.TableId, source.RowVersion, target.TableId, target.RowVersion, "Move", stale));
        AssertRecent(transfer.TransferredAt);

        var primary = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-46");
        var participant = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-47");
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", participant.TableId));
        await _database.SeedOpenOrderAndBillAsync(participant.TableId);
        var merge = await PostAsync<TableMergeResult>(client, Prefix(terminalId) + "/merges", cookie,
            new MergeTablesRequest(primary.TableId, primary.RowVersion, [new TableMergeParticipantRequest(participant.TableId, participant.RowVersion)], "Join"));
        var unmerge = await PostAsync<TableUnmergeResult>(client, $"{Prefix(terminalId)}/merges/{merge.MergeGroupId:D}/unmerge", cookie,
            new UnmergeTablesRequest(
                merge.NewPrimaryRowVersion,
                [new TableMergeParticipantRequest(participant.TableId, merge.NewParticipantRowVersions[participant.TableId])],
                "Split",
                stale));
        AssertRecent(unmerge.UnmergedAt);
    }

    [Fact]
    public async Task AnOccupiedTableIsNotOfferedTheReserveCommand()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var zone = await PostAsync<ZoneDto>(client, Prefix(terminalId) + "/zones", cookie, new CreateZoneRequest(0, "ZSR2", "Hall SR2"));
        var free = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-11");
        var busy = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-12");
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", busy.TableId));

        var tables = await GetAsync<List<TableDto>>(client, $"{Prefix(terminalId)}/tables?zoneId={zone.ZoneId:D}", cookie);

        Assert.Contains("Reserve", tables.Single(t => t.TableId == free.TableId).AllowedCommands);
        Assert.DoesNotContain("Reserve", tables.Single(t => t.TableId == busy.TableId).AllowedCommands);
    }

    [Fact]
    public async Task TheQuickSaleAnchorTableIsNotListedAsAFloorTable()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        await Exec(
            """
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, row_version)
            VALUES ('00000000-0000-0000-0000-000000000001', NULL, 'KASA-1', 1, true, 'Available', 1)
            ON CONFLICT (table_id) DO NOTHING;
            """);

        var tables = await GetAsync<List<TableDto>>(client, $"{Prefix(terminalId)}/tables", cookie);

        Assert.DoesNotContain(tables, t => t.TableNumber == "KASA-1");
    }

    [Fact]
    public async Task ManualExpiryOfAReservationBeforeItsEndTimeIsAConflict()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var zone = await PostAsync<ZoneDto>(client, Prefix(terminalId) + "/zones", cookie, new CreateZoneRequest(0, "ZSR3", "Hall SR3"));
        var table = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-21");
        var reservation = await PostAsync<TableReservationResult>(client, Prefix(terminalId) + "/reservations", cookie,
            new CreateTableReservationRequest(table.TableId, table.RowVersion, null, "Phone", 2, null, DateTimeOffset.UtcNow.AddHours(2)));

        using var request = JsonRequest(HttpMethod.Post, $"{Prefix(terminalId)}/reservations/{reservation.ReservationId:D}/expire", cookie,
            new ExpireTableReservationRequest(reservation.ReservationRowVersion, reservation.NewTableRowVersion, "Early"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RESERVATION_NOT_DUE", (await response.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>())!.Error.Code);
    }

    [Fact]
    public async Task AReservationEndTimeInThePastIsRejected()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var zone = await PostAsync<ZoneDto>(client, Prefix(terminalId) + "/zones", cookie, new CreateZoneRequest(0, "ZSR4", "Hall SR4"));
        var table = await CreateTableInZoneAsync(client, terminalId, cookie, zone, "S-31");

        using var request = JsonRequest(HttpMethod.Post, Prefix(terminalId) + "/reservations", cookie,
            new CreateTableReservationRequest(table.TableId, table.RowVersion, null, "Phone", 2, null, DateTimeOffset.UtcNow.AddHours(-1)));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpClient Client, WebApplication App, Guid TerminalId, string Cookie)> StartSessionAsync()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddTableManagementExperience();
        var app = builder.Build();
        app.MapTableManagementApi();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return (new HttpClient { BaseAddress = new Uri(address) }, app, terminalId, cookie);
    }

    private static void AssertRecent(DateTimeOffset value)
        => Assert.True(value > DateTimeOffset.UtcNow.AddMinutes(-1), $"Expected a server time, got {value:O}");

    private static Task<TableDto> CreateTableInZoneAsync(HttpClient client, Guid terminalId, string cookie, ZoneDto zone, string number)
        => PostAsync<TableDto>(client, Prefix(terminalId) + "/tables", cookie, new CreateTableRequest(0, number, zone.ZoneId, 4));

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _database.Source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<TResponse> PostAsync<TResponse>(HttpClient client, string path, string cookie, object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<TResponse> GetAsync<TResponse>(HttpClient client, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string cookie, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static string Prefix(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/table-management";
}
