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
public sealed class FloorPlanHttpTests : IAsyncLifetime
{
    private readonly TableManagementTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AuthorizedSavePersistsStableSeatsAndStaleVersionLeavesNoPartialChange()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, canMutate: false, expired: false);
        var zoneId = Guid.Empty;
        var tableId = Guid.Empty;
        var seatId = Guid.NewGuid();
        var secondSeatId = Guid.NewGuid();

        await using (var app = await StartAsync())
        using (var client = CreateClient(app))
        {
            var zone = await PostAsync<ZoneDto>(
                client,
                Prefix(terminalId) + "/zones",
                cookie,
                new CreateZoneRequest(0, "MAIN", "Main Floor"));
            zoneId = zone.ZoneId;
            var table = await PostAsync<TableDto>(
                client,
                Prefix(terminalId) + "/tables",
                cookie,
                new CreateTableRequest(0, "A-01", zone.ZoneId, 2));
            tableId = table.TableId;

            using (var missingSession = await client.GetAsync(FloorPath(terminalId, zone.ZoneId)))
                Assert.Equal(HttpStatusCode.Unauthorized, missingSession.StatusCode);

            var createRequest = new SaveFloorPlanRequest(
                0,
                1_280,
                800,
                [new SaveFloorTableRequest(
                    table.TableId,
                    table.RowVersion,
                    0,
                    120,
                    100,
                    160,
                    96,
                    "Rectangle",
                    0,
                    [
                        new SaveFloorSeatRequest(seatId, 0, 1, "Seat 1", 120, 80),
                        new SaveFloorSeatRequest(secondSeatId, 0, 2, "Seat 2", 200, 80),
                    ])]);

            using (var deniedRequest = JsonRequest(HttpMethod.Put, FloorPath(terminalId, zone.ZoneId), readOnlyCookie, createRequest))
            using (var denied = await client.SendAsync(deniedRequest))
            {
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }

            var created = await PutAsync<SaveFloorPlanResponse>(
                client,
                FloorPath(terminalId, zone.ZoneId),
                cookie,
                createRequest);
            Assert.Equal(1, created.FloorPlan.RowVersion);
            Assert.Empty(created.Warnings);
            var createdTable = Assert.Single(created.FloorPlan.Tables);
            Assert.Equal(1, createdTable.LayoutRowVersion);
            Assert.Equal([seatId, secondSeatId], createdTable.Seats.Select(seat => seat.SeatId));
            Assert.Contains("Reserve", createdTable.AllowedCommands);

            using (var staleRequest = JsonRequest(HttpMethod.Put, FloorPath(terminalId, zone.ZoneId), cookie, createRequest))
            using (var stale = await client.SendAsync(staleRequest))
            {
                Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
                Assert.Equal(
                    "CONCURRENT_MODIFICATION",
                    (await stale.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>())!.Error.Code);
            }
            Assert.Equal(120, await _database.ScalarAsync<int>(
                $"SELECT x FROM table_mgmt.table_layouts WHERE table_id = '{table.TableId:D}';"));

            using (var tableUpdateRequest = JsonRequest(
                HttpMethod.Put,
                $"{Prefix(terminalId)}/tables/{table.TableId:D}",
                cookie,
                new UpdateTableRequest(table.RowVersion, table.TableNumber, zone.ZoneId, table.Capacity, true)))
            using (var tableUpdate = await client.SendAsync(tableUpdateRequest))
            {
                Assert.Equal(HttpStatusCode.OK, tableUpdate.StatusCode);
                table = (await tableUpdate.Content.ReadFromJsonAsync<TableDto>())!;
            }

            using (var staleTableVersionRequest = JsonRequest(
                HttpMethod.Put,
                FloorPath(terminalId, zone.ZoneId),
                cookie,
                createRequest with
                {
                    ExpectedRowVersion = created.FloorPlan.RowVersion,
                    Tables = [createRequest.Tables[0] with
                    {
                        ExpectedLayoutRowVersion = createdTable.LayoutRowVersion,
                        X = 200,
                    }],
                }))
            using (var staleTableVersion = await client.SendAsync(staleTableVersionRequest))
            {
                Assert.Equal(HttpStatusCode.Conflict, staleTableVersion.StatusCode);
            }
            Assert.Equal(120, await _database.ScalarAsync<int>(
                $"SELECT x FROM table_mgmt.table_layouts WHERE table_id = '{table.TableId:D}';"));

            var updated = await PutAsync<SaveFloorPlanResponse>(
                client,
                FloorPath(terminalId, zone.ZoneId),
                cookie,
                createRequest with
                {
                    ExpectedRowVersion = created.FloorPlan.RowVersion,
                    Tables = [createRequest.Tables[0] with
                    {
                        ExpectedTableRowVersion = table.RowVersion,
                        ExpectedLayoutRowVersion = createdTable.LayoutRowVersion,
                        X = 240,
                        Seats = [
                            createRequest.Tables[0].Seats[0] with
                            {
                                ExpectedRowVersion = createdTable.Seats[0].RowVersion,
                                Number = 2,
                                X = 240,
                            },
                            createRequest.Tables[0].Seats[1] with
                            {
                                ExpectedRowVersion = createdTable.Seats[1].RowVersion,
                                Number = 1,
                                X = 280,
                            },
                        ],
                    }],
                });
            Assert.Equal(2, updated.FloorPlan.RowVersion);
            Assert.Equal(2, updated.FloorPlan.Tables[0].LayoutRowVersion);
            Assert.All(updated.FloorPlan.Tables[0].Seats, seat => Assert.Equal(2, seat.RowVersion));
        }

        await using (var restartedApp = await StartAsync())
        using (var restartedClient = CreateClient(restartedApp))
        using (var readRequest = Request(HttpMethod.Get, FloorPath(terminalId, zoneId), cookie))
        using (var read = await restartedClient.SendAsync(readRequest))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var persisted = await read.Content.ReadFromJsonAsync<FloorPlanDto>();
            Assert.Equal(240, Assert.Single(persisted!.Tables).X);
            Assert.Equal(tableId, persisted.Tables[0].TableId);
            Assert.Equal([secondSeatId, seatId], persisted.Tables[0].Seats.Select(seat => seat.SeatId));
        }
    }

    [Fact]
    public async Task NonMergeOverlapRollsBackAndActiveMergeAllowsOverlapWithCapacityWarning()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, canMutate: true, expired: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var zone = await PostAsync<ZoneDto>(
            client,
            Prefix(terminalId) + "/zones",
            cookie,
            new CreateZoneRequest(0, "MERGE", "Merge Floor"));
        var primary = await CreateTableAsync(client, terminalId, cookie, zone.ZoneId, "M-01", 4);
        var merged = await CreateTableAsync(client, terminalId, cookie, zone.ZoneId, "M-02", 2);
        var request = new SaveFloorPlanRequest(
            0,
            1_280,
            800,
            [
                new SaveFloorTableRequest(primary.TableId, primary.RowVersion, 0, 100, 100, 160, 96, "Rectangle", 0, []),
                new SaveFloorTableRequest(merged.TableId, merged.RowVersion, 0, 180, 120, 160, 96, "Rectangle", 0, []),
            ]);

        using (var overlapRequest = JsonRequest(HttpMethod.Put, FloorPath(terminalId, zone.ZoneId), cookie, request))
        using (var overlap = await client.SendAsync(overlapRequest))
        {
            Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
            Assert.Equal("VALIDATION_FAILED", (await overlap.Content.ReadFromJsonAsync<TableManagementErrorEnvelope>())!.Error.Code);
        }
        Assert.Equal(0L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM table_mgmt.zone_floor_plans WHERE zone_id = '{zone.ZoneId:D}';"));

        var actorId = await _database.ScalarAsync<Guid>("SELECT user_id FROM identity.users ORDER BY username LIMIT 1;");
        await using (var dataSource = NpgsqlDataSource.Create(_database.ConnectionString))
        await using (var command = dataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.table_merges (
                table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                status, reason, merged_by, merged_at)
            VALUES (
                @record_id, @group_id, @primary_id, @merged_id,
                'Active', 'Floor plan overlap test', @actor_id, now());
            """))
        {
            command.Parameters.AddWithValue("record_id", Guid.NewGuid());
            command.Parameters.AddWithValue("group_id", Guid.NewGuid());
            command.Parameters.AddWithValue("primary_id", primary.TableId);
            command.Parameters.AddWithValue("merged_id", merged.TableId);
            command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync();
        }

        var saved = await PutAsync<SaveFloorPlanResponse>(
            client,
            FloorPath(terminalId, zone.ZoneId),
            cookie,
            request);
        Assert.Equal(2, saved.Warnings.Count);
        Assert.All(saved.Warnings, warning => Assert.Equal("CAPACITY_SEAT_MISMATCH", warning.Code));
        Assert.All(saved.FloorPlan.Tables, table => Assert.NotNull(table.MergeGroupId));
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

    private static Task<TableDto> CreateTableAsync(
        HttpClient client,
        Guid terminalId,
        string cookie,
        Guid zoneId,
        string tableNumber,
        int capacity)
        => PostAsync<TableDto>(
            client,
            Prefix(terminalId) + "/tables",
            cookie,
            new CreateTableRequest(0, tableNumber, zoneId, capacity));

    private static async Task<TResponse> PostAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie,
        object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<TResponse> PutAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie,
        object body)
    {
        using var request = JsonRequest(HttpMethod.Put, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string cookie, object body)
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
        => $"/api/v1/terminals/{terminalId:D}/table-management";

    private static string FloorPath(Guid terminalId, Guid zoneId)
        => $"{Prefix(terminalId)}/floor-plans/{zoneId:D}";
}
