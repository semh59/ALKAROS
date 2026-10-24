using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Tables;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Tables;

[Collection("Table management PostgreSQL HTTP")]
public sealed class TableGuardsHttpTests : IAsyncLifetime
{
    private readonly TableManagementTestDatabase _database = new();
    private NpgsqlDataSource Source => _database.Source;

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ATableWithASubmittedOrderCannotBeReleasedByHand()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var table = await CreateTableAsync(client, terminalId, cookie, "G-01");
        await _database.SeedCurrentOrderWithItemsAsync(table.TableId, (10m, "Active"));
        await Exec("UPDATE orders.orders SET status = 'Submitted' WHERE order_id = (SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @id);", ("id", table.TableId));
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", table.TableId));

        var error = await PostExpectingConflictAsync(client, StatusPath(terminalId, table.TableId), cookie, new ChangeTableStatusRequest(table.RowVersion, "Available"));

        Assert.Equal("TABLE_HAS_OPEN_WORK", error.Code);
        Assert.Equal("Occupied", await StatusOfAsync(table.TableId));
    }

    [Fact]
    public async Task ATableWithOnlyAnEmptyDraftIsReleasedAndTheChangeIsAudited()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var table = await CreateTableAsync(client, terminalId, cookie, "G-02");
        await _database.SeedCurrentOrderWithItemsAsync(table.TableId);
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", table.TableId));

        using var request = JsonRequest(HttpMethod.Post, StatusPath(terminalId, table.TableId), cookie, new ChangeTableStatusRequest(table.RowVersion, "Available"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM audit.audit_events WHERE event_name = 'tables.table.status_changed' AND aggregate_id = '{table.TableId:D}' AND before_state_json->>'Status' = 'Occupied' AND after_state_json->>'Status' = 'Available' AND actor_id IS NOT NULL;"));
    }

    [Fact]
    public async Task ATableInAnActiveMergeCannotBeReleasedOrSwitchedOff()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var leader = await CreateTableAsync(client, terminalId, cookie, "G-03");
        var follower = await CreateTableAsync(client, terminalId, cookie, "G-04");
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id IN (@a, @b);", ("a", leader.TableId), ("b", follower.TableId));
        await Exec(
            """
            INSERT INTO table_mgmt.table_merges (table_merge_id, merge_group_id, primary_table_id, merged_table_id, status, reason, merged_by, merged_at, row_version)
            VALUES (gen_random_uuid(), gen_random_uuid(), @a, @b, 'Active', 'Joined', (SELECT user_id FROM identity.users LIMIT 1), now(), 1);
            """, ("a", leader.TableId), ("b", follower.TableId));

        var release = await PostExpectingConflictAsync(client, StatusPath(terminalId, follower.TableId), cookie, new ChangeTableStatusRequest(follower.RowVersion, "Available"));
        Assert.Equal("TABLE_IN_MERGE", release.Code);

        var switchOff = await PutExpectingConflictAsync(client, $"{Prefix(terminalId)}/tables/{leader.TableId:D}", cookie,
            new UpdateTableRequest(leader.RowVersion, "G-03", leader.ZoneId, leader.Capacity, false));
        Assert.Equal("TABLE_IN_MERGE", switchOff.Code);
    }

    [Fact]
    public async Task ABusyTableCannotBeSwitchedOffButAFreeOneCan()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var busy = await CreateTableAsync(client, terminalId, cookie, "G-05");
        var free = await CreateTableAsync(client, terminalId, cookie, "G-06");
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", busy.TableId));

        var refused = await PutExpectingConflictAsync(client, $"{Prefix(terminalId)}/tables/{busy.TableId:D}", cookie,
            new UpdateTableRequest(busy.RowVersion, "G-05", busy.ZoneId, busy.Capacity, false));
        Assert.Equal("TABLE_BUSY", refused.Code);

        using var request = JsonRequest(HttpMethod.Put, $"{Prefix(terminalId)}/tables/{free.TableId:D}", cookie,
            new UpdateTableRequest(free.RowVersion, "G-06", free.ZoneId, free.Capacity, false));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ZoneTableAndFloorPlanWritesAreAudited()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var table = await CreateTableAsync(client, terminalId, cookie, "G-07");

        using (var update = JsonRequest(HttpMethod.Put, $"{Prefix(terminalId)}/tables/{table.TableId:D}", cookie,
            new UpdateTableRequest(table.RowVersion, "G-07B", table.ZoneId, table.Capacity, true)))
        using (var response = await client.SendAsync(update))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var current = await GetTableRowVersionAsync(table.TableId);
        using (var save = JsonRequest(HttpMethod.Put, $"{Prefix(terminalId)}/floor-plans/{table.ZoneId:D}", cookie,
            new SaveFloorPlanRequest(0, 1_280, 800,
                [new SaveFloorTableRequest(table.TableId, current, 0, 120, 100, 160, 96, "Rectangle", 0, [])])))
        using (var response = await client.SendAsync(save))
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        foreach (var name in new[] { "tables.zone.created", "tables.table.created", "tables.table.updated", "tables.floor_plan.saved" })
        {
            Assert.Equal(1L, await _database.ScalarAsync<long>(
                $"SELECT count(*) FROM audit.audit_events WHERE event_name = '{name}' AND actor_id IS NOT NULL;"));
        }
    }

    [Fact]
    public async Task TheStatusTimestampMovesOnAnyStatusWriteAndOnlyThen()
    {
        var (client, app, terminalId, cookie) = await StartSessionAsync();
        await using var _ = app;
        using var __ = client;
        var table = await CreateTableAsync(client, terminalId, cookie, "G-08");
        await Exec("UPDATE table_mgmt.tables SET status_changed_at = now() - interval '2 hours' WHERE table_id = @id;", ("id", table.TableId));

        await Exec("UPDATE table_mgmt.tables SET capacity = 6 WHERE table_id = @id;", ("id", table.TableId));
        Assert.True(await _database.ScalarAsync<bool>($"SELECT status_changed_at < now() - interval '1 hour' FROM table_mgmt.tables WHERE table_id = '{table.TableId:D}';"));

        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", table.TableId));
        Assert.True(await _database.ScalarAsync<bool>($"SELECT status_changed_at > now() - interval '1 minute' FROM table_mgmt.tables WHERE table_id = '{table.TableId:D}';"));

        // Writing the status it already has is not a change.
        await Exec("UPDATE table_mgmt.tables SET status_changed_at = now() - interval '2 hours' WHERE table_id = @id;", ("id", table.TableId));
        await Exec("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;", ("id", table.TableId));
        Assert.True(await _database.ScalarAsync<bool>($"SELECT status_changed_at < now() - interval '1 hour' FROM table_mgmt.tables WHERE table_id = '{table.TableId:D}';"));
    }

    private async Task<(HttpClient Client, WebApplication App, Guid TerminalId, string Cookie)> StartSessionAsync()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        var app = await StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return (new HttpClient { BaseAddress = new Uri(address) }, app, terminalId, cookie);
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddTableManagementExperience();
        var app = builder.Build();
        app.MapTableManagementApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<TableDto> CreateTableAsync(HttpClient client, Guid terminalId, string cookie, string number)
    {
        var zone = await PostAsync<ZoneDto>(client, Prefix(terminalId) + "/zones", cookie, new CreateZoneRequest(0, "Z" + number.Replace("-", ""), "Hall " + number));
        return await PostAsync<TableDto>(client, Prefix(terminalId) + "/tables", cookie, new CreateTableRequest(0, number, zone.ZoneId, 4));
    }

    private async Task<long> GetTableRowVersionAsync(Guid tableId)
        => await _database.ScalarAsync<long>($"SELECT row_version FROM table_mgmt.tables WHERE table_id = '{tableId:D}';");

    private async Task<string> StatusOfAsync(Guid tableId)
        => await _database.ScalarAsync<string>($"SELECT current_status FROM table_mgmt.tables WHERE table_id = '{tableId:D}';");

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Source.CreateCommand(sql);
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

    private static Task<TableManagementError> PostExpectingConflictAsync(HttpClient client, string path, string cookie, object body)
        => ExpectConflictAsync(client, HttpMethod.Post, path, cookie, body);

    private static Task<TableManagementError> PutExpectingConflictAsync(HttpClient client, string path, string cookie, object body)
        => ExpectConflictAsync(client, HttpMethod.Put, path, cookie, body);

    private static async Task<TableManagementError> ExpectConflictAsync(HttpClient client, HttpMethod method, string path, string cookie, object body)
    {
        using var request = JsonRequest(method, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>())!.Error;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string cookie, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static string Prefix(Guid terminalId) => $"/api/v1/terminals/{terminalId:D}/table-management";

    private static string StatusPath(Guid terminalId, Guid tableId) => $"{Prefix(terminalId)}/tables/{tableId:D}/status";
}
