using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Tables;
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
public sealed class TableIdempotencyHttpTests : IAsyncLifetime
{
    private readonly TableManagementTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ARepeatedTransferWithTheSameKeyAnswersWithTheFirstResultAndMovesOnce()
    {
        await using var session = await StartSessionAsync();
        var (source, target) = await OccupiedAndFreeTableAsync(session, "ID1");
        var body = new TransferTableRequest(source.TableId, source.RowVersion, target.TableId, target.RowVersion, "Move");

        var first = await SendAsync(session, "/transfers", body, "key-transfer-1");
        var again = await SendAsync(session, "/transfers", body, "key-transfer-1");

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal(first.Body, again.Body);
        Assert.Equal(1L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM table_mgmt.table_transfers WHERE source_table_id = '{source.TableId:D}';"));
    }

    [Fact]
    public async Task WithoutAKeyARepeatedTransferStillConflicts()
    {
        await using var session = await StartSessionAsync();
        var (source, target) = await OccupiedAndFreeTableAsync(session, "ID2");
        var body = new TransferTableRequest(source.TableId, source.RowVersion, target.TableId, target.RowVersion, "Move");

        var first = await SendAsync(session, "/transfers", body, null);
        var again = await SendAsync(session, "/transfers", body, null);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.Conflict, again.Status);
    }

    [Fact]
    public async Task TheSameKeyWithADifferentRequestIsRefused()
    {
        await using var session = await StartSessionAsync();
        var (source, target) = await OccupiedAndFreeTableAsync(session, "ID3");
        var other = await CreateTableAsync(session, "ID3-C");
        var body = new TransferTableRequest(source.TableId, source.RowVersion, target.TableId, target.RowVersion, "Move");

        await SendAsync(session, "/transfers", body, "key-transfer-3");
        var reused = await SendAsync(session, "/transfers", body with { TargetTableId = other.TableId, ExpectedTargetRowVersion = other.RowVersion }, "key-transfer-3");

        Assert.Equal(HttpStatusCode.Conflict, reused.Status);
        Assert.Contains("IDEMPOTENCY_KEY_REUSED", reused.Body);
    }

    [Fact]
    public async Task ARepeatedUnmergeWithTheSameKeyDoesNotFailWith404()
    {
        await using var session = await StartSessionAsync();
        var primary = await CreateTableAsync(session, "ID4-P");
        var joined = await CreateTableAsync(session, "ID4-J");
        var merge = await PostAsync<TableMergeResult>(session, "/merges",
            new MergeTablesRequest(primary.TableId, primary.RowVersion, [new TableMergeParticipantRequest(joined.TableId, joined.RowVersion)], "Group"));
        var body = new UnmergeTablesRequest(
            merge.NewPrimaryRowVersion,
            [new TableMergeParticipantRequest(joined.TableId, merge.NewParticipantRowVersions[joined.TableId])],
            "Split");
        var path = $"/merges/{merge.MergeGroupId:D}/unmerge";

        var first = await SendAsync(session, path, body, "key-unmerge-4");
        var again = await SendAsync(session, path, body, "key-unmerge-4");
        var withoutKey = await SendAsync(session, path, body, null);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal(first.Body, again.Body);
        Assert.NotEqual(HttpStatusCode.OK, withoutKey.Status);
    }

    [Fact]
    public async Task TwoMergesSentAtOnceWithOneKeyRunOnceAndBothAnswerOk()
    {
        await using var session = await StartSessionAsync();
        var primary = await CreateTableAsync(session, "ID5-P");
        var joined = await CreateTableAsync(session, "ID5-J");
        var body = new MergeTablesRequest(primary.TableId, primary.RowVersion, [new TableMergeParticipantRequest(joined.TableId, joined.RowVersion)], "Group");

        var results = await Task.WhenAll(
            SendAsync(session, "/merges", body, "key-merge-5"),
            SendAsync(session, "/merges", body, "key-merge-5"));

        Assert.All(results, result => Assert.Equal(HttpStatusCode.OK, result.Status));
        Assert.Equal(results[0].Body, results[1].Body);
        Assert.Equal(1L, await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM table_mgmt.table_merges WHERE primary_table_id = '{primary.TableId:D}';"));
    }

    [Fact]
    public async Task AKeyIsScopedToTheSignedInUserAndTerminal()
    {
        await using var session = await StartSessionAsync();
        var (source, target) = await OccupiedAndFreeTableAsync(session, "ID6");
        var body = new TransferTableRequest(source.TableId, source.RowVersion, target.TableId, target.RowVersion, "Move");
        await SendAsync(session, "/transfers", body, "key-transfer-6");

        var stored = await _database.ScalarAsync<string>(
            "SELECT client_id FROM idempotency_keys WHERE operation_id = 'key-transfer-6';");

        Assert.Matches($"^tables:{session.TerminalId:N}:[0-9a-f]{{32}}$", stored);
    }

    [Fact]
    public async Task AnExpiredKeyNoLongerReplaysTheFirstAnswer()
    {
        await using var session = await StartSessionAsync();
        var (source, target) = await OccupiedAndFreeTableAsync(session, "ID7");
        var body = new TransferTableRequest(source.TableId, source.RowVersion, target.TableId, target.RowVersion, "Move");
        await SendAsync(session, "/transfers", body, "key-transfer-7");
        await using (var expire = _database.Source.CreateCommand(
            "UPDATE idempotency_keys SET expires_at = now() - interval '1 minute' WHERE operation_id = 'key-transfer-7';"))
            await expire.ExecuteNonQueryAsync();

        var again = await SendAsync(session, "/transfers", body, "key-transfer-7");

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
    }

    private async Task<(TableDto Source, TableDto Target)> OccupiedAndFreeTableAsync(Session session, string prefix)
    {
        var source = await CreateTableAsync(session, prefix + "-S");
        var target = await CreateTableAsync(session, prefix + "-T");
        await using (var command = _database.Source.CreateCommand("UPDATE table_mgmt.tables SET current_status = 'Occupied' WHERE table_id = @id;"))
        {
            command.Parameters.AddWithValue("id", source.TableId);
            await command.ExecuteNonQueryAsync();
        }

        return (source, target);
    }

    private static async Task<TableDto> CreateTableAsync(Session session, string number)
    {
        var zone = session.Zone ??= await PostAsync<ZoneDto>(session, "/zones", new CreateZoneRequest(0, "ZID" + Guid.NewGuid().ToString("N")[..6], "Hall ID"));
        return await PostAsync<TableDto>(session, "/tables", new CreateTableRequest(0, number, zone.ZoneId, 4));
    }

    private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(Session session, string path, object body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, session.Prefix + path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Cookie", session.Cookie);
        if (key is not null)
            request.Headers.Add("X-Idempotency-Key", key);
        using var response = await session.Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<TResponse> PostAsync<TResponse>(Session session, string path, object body)
    {
        var (status, text) = await SendAsync(session, path, body, null);
        Assert.True((int)status < 300, text);
        return System.Text.Json.JsonSerializer.Deserialize<TResponse>(text, WebJson)!;
    }

    private async Task<Session> StartSessionAsync()
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
        return new Session(new HttpClient { BaseAddress = new Uri(address) }, app, terminalId, cookie);
    }

    private sealed class Session(HttpClient client, WebApplication app, Guid terminalId, string cookie) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public Guid TerminalId { get; } = terminalId;
        public string Cookie { get; } = cookie;
        public string Prefix => $"/api/v1/terminals/{TerminalId:D}/table-management";
        public ZoneDto? Zone { get; set; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
