using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.OfflineQueueReport;
using ALKAROS.Host.Observability.ServerDetectorsDeep;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.OfflineQueueReport.Tests;

public sealed class QueueDatabase : PgTestDatabase
{
    public QueueDatabase() : base("alkaros_rmd566_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var file = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-533"), "187-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        await RunAsync(DataSource, "CREATE TABLE public.plan_probe (id int); INSERT INTO public.plan_probe VALUES (1);");
    }
}

public sealed class OfflineQueueReportTests : IClassFixture<QueueDatabase>
{
    private readonly QueueDatabase _database;

    public OfflineQueueReportTests(QueueDatabase database) => _database = database;

    private ErrorEventWriter NewWriter() => new(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

    private Task<int> ResetAsync() =>
        _database.ExecuteAsync("DELETE FROM observability.error_occurrences; DELETE FROM observability.error_events;");

    private Task<long> CountAsync(string where) =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE {where};");

    private static async Task WaitAsync(Func<bool> done)
    {
        for (var attempt = 0; attempt < 100 && !done(); attempt++)
            await Task.Delay(50);
    }

    [Fact]
    public async Task QueueOverTheLimitRaisesOneEventResolvesBelowItAndReopensWhenItGrowsAgain()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        var ingest = new OfflineQueueIngest(writer, _database.DataSource, limit: 20);
        var terminal = Guid.NewGuid();

        (await ingest.AcceptAsync(new QueueLengthReport(25), terminal, default)).Should().Be(OfflineQueueVerdict.Accepted);
        (await ingest.AcceptAsync(new QueueLengthReport(31), terminal, default)).Should().Be(OfflineQueueVerdict.Accepted);
        await writer.FlushAsync();
        (await CountAsync("category = 'browser.offline_queue_backlog' AND status = 'Open'")).Should().Be(1);
        (await CountAsync("category = 'browser.offline_queue_backlog'")).Should().Be(1);

        await ingest.AcceptAsync(new QueueLengthReport(19), terminal, default);
        (await CountAsync("category = 'browser.offline_queue_backlog' AND status = 'Resolved'")).Should().Be(1);

        await ingest.AcceptAsync(new QueueLengthReport(20), terminal, default);
        await writer.FlushAsync();
        (await CountAsync("category = 'browser.offline_queue_backlog' AND status = 'Open'")).Should().Be(1);
        (await CountAsync("category = 'browser.offline_queue_backlog'")).Should().Be(1);
    }

    [Fact]
    public async Task EachTerminalHasItsOwnEventAndTheEventCarriesNoCount()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        var ingest = new OfflineQueueIngest(writer, _database.DataSource, limit: 20);

        await ingest.AcceptAsync(new QueueLengthReport(25), Guid.NewGuid(), default);
        await ingest.AcceptAsync(new QueueLengthReport(25), Guid.NewGuid(), default);
        await writer.FlushAsync();

        (await CountAsync("category = 'browser.offline_queue_backlog'")).Should().Be(2);
        (await CountAsync("category = 'browser.offline_queue_backlog' AND detail ~ '[0-9]{2}\\s'")).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task ImplausibleLengthIsRejectedWithoutAnEvent(int? length)
    {
        await ResetAsync();
        await using var writer = NewWriter();
        var ingest = new OfflineQueueIngest(writer, _database.DataSource, limit: 20);

        (await ingest.AcceptAsync(new QueueLengthReport(length), Guid.NewGuid(), default)).Should().Be(OfflineQueueVerdict.Invalid);
        await writer.FlushAsync();

        (await CountAsync("true")).Should().Be(0);
    }

    [Fact]
    public async Task ReportWithoutASessionIsRejectedWith401()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var writer = NewWriter();
        builder.Services.AddSingleton(new DualScreenStore(_database.DataSource));
        builder.Services.AddSingleton(new OfflineQueueIngest(writer, _database.DataSource));
        await using var app = builder.Build();
        app.MapOfflineQueueReport();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await client.PostAsJsonAsync("/api/v1/telemetry/offline-queue", new QueueLengthReport(50));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("https://api.telegram.org/bot123456:SECRET-token/sendMessage?chat_id=1&key=abc", "api.telegram.org/:id/sendMessage")]
    [InlineData("https://user:pw@host.example:8443/v1/orders/9f8b2c1e-1111-2222-3333-444455556666?apikey=zzz#frag", "host.example/v1/orders/:id")]
    [InlineData("https://Host.Example/orders/12345/lines", "host.example/orders/:id/lines")]
    [InlineData("https://host.example", "host.example")]
    [InlineData("ftp://host.example/file", "unknown")]
    [InlineData("/relative/path", "unknown")]
    [InlineData(null, "unknown")]
    public void AddressKeepsOnlyHostAndPlainWordSegments(string? address, string expected) =>
        ExternalAddress.Sanitize(address).Should().Be(expected);

    private static async Task<WebApplication> StartRemoteAsync(int status, int delayMilliseconds)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Map("/{**path}", async (HttpContext context) =>
        {
            await Task.Delay(delayMilliseconds);
            context.Response.StatusCode = status;
        });
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task FailedAndSlowOutgoingCallsBecomeEventsWithSanitizedAddressesAndFastOnesDoNot()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var tap = new ExternalCallTap(writer, new ExternalCallOptions { SlowWarnMilliseconds = 150, SlowErrorMilliseconds = 60_000 });
        await using var failing = await StartRemoteAsync(503, 0);
        await using var slow = await StartRemoteAsync(200, 300);
        await using var fast = await StartRemoteAsync(200, 0);
        using var client = new HttpClient();

        using (await client.GetAsync($"{failing.Urls.First()}/bot123456:SECRET-token/send?key=abc123")) { }
        using (await client.GetAsync($"{slow.Urls.First()}/v1/invoices/9f8b2c1e-1111-2222-3333-444455556666?token=t0k3n")) { }
        using (await client.GetAsync($"{fast.Urls.First()}/v1/ping")) { }
        await writer.FlushAsync();

        (await CountAsync("category = 'integration.external_call_failed' AND layer = 'Integration'")).Should().Be(1);
        (await CountAsync("category = 'integration.external_call_slow' AND severity = 'Warning'")).Should().Be(1);
        (await CountAsync("category LIKE 'integration.external_call_%'")).Should().Be(2);
        (await _database.ScalarAsync<string>(
            "SELECT detail FROM observability.error_events WHERE category = 'integration.external_call_failed';"))
            .Should().Be("GET 127.0.0.1/:id/send");
        (await _database.ScalarAsync<string>(
            "SELECT detail FROM observability.error_events WHERE category = 'integration.external_call_slow';"))
            .Should().Be("GET 127.0.0.1/v1/invoices/:id");
        (await CountAsync("detail ~* '(SECRET|abc123|t0k3n|key=|token=|\\?|9f8b2c1e|123456)'")).Should().Be(0);
    }

    [Fact]
    public async Task CallThatGetsNoAnswerIsAnErrorEventAndAClientMistakeIsOnlyAWarning()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var tap = new ExternalCallTap(writer);
        await using var rejecting = await StartRemoteAsync(401, 0);
        using var client = new HttpClient();

        using (await client.GetAsync($"{rejecting.Urls.First()}/v1/login")) { }
        var closed = new Uri(rejecting.Urls.First()).Port;
        await rejecting.StopAsync();
        var act = () => client.GetAsync($"http://127.0.0.1:{closed}/v1/other?secret=abc");
        await act.Should().ThrowAsync<HttpRequestException>();
        await writer.FlushAsync();

        (await CountAsync("category = 'integration.external_call_failed' AND severity = 'Warning'")).Should().Be(1);
        (await CountAsync("category = 'integration.external_call_failed' AND severity = 'Error'")).Should().Be(1);
        (await CountAsync("detail LIKE '%secret%'")).Should().Be(0);
    }

    [Theory]
    [InlineData("SELECT id FROM t WHERE id = @id", "SELECT id FROM t WHERE id = $1")]
    [InlineData("select a from t where x = @from and y = @to and z = @from;", "select a from t where x = $1 and y = $2 and z = $1")]
    [InlineData("SELECT '@name' AS literal, id FROM t WHERE id = @id", "SELECT '@name' AS literal, id FROM t WHERE id = $1")]
    [InlineData("  SELECT payload @> '{}' FROM t", "SELECT payload @> '{}' FROM t")]
    public void SelectStatementsGetPositionalPlaceholders(string statement, string expected) =>
        SlowQueryPlanner.ToExplainable(statement)!.Value.Sql.Should().Be(expected);

    [Theory]
    [InlineData("INSERT INTO t VALUES (@a)")]
    [InlineData("UPDATE t SET a = @a")]
    [InlineData("WITH x AS (DELETE FROM t RETURNING *) SELECT * FROM x")]
    [InlineData("SELECT 1; DROP TABLE t")]
    [InlineData("EXPLAIN SELECT 1")]
    public void OnlyASingleSelectIsPlanned(string statement) =>
        SlowQueryPlanner.ToExplainable(statement).Should().BeNull();

    [Fact]
    public async Task SlowSelectWithNamedParameterGetsAPlanEventWithoutCostsOrFilterText()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var planner = new SlowQueryPlanner(writer, _database.DataSource);
        using var tap = new DbActivityTap(writer, new ServerDetectorsDeepOptions { SlowWarnMilliseconds = 100 }, planner);

        await using (var command = _database.DataSource.CreateCommand("SELECT id FROM public.plan_probe, pg_sleep(0.3) WHERE id = @id"))
        {
            command.Parameters.AddWithValue("id", 1);
            await command.ExecuteScalarAsync();
        }

        await WaitAsync(() => planner.Planned + planner.Failures > 0);
        await writer.FlushAsync();

        planner.Failures.Should().Be(0);
        var detail = await _database.ScalarAsync<string>(
            "SELECT detail FROM observability.error_events WHERE category = 'db.slow_query_plan';");
        detail.Should().Contain("=>").And.Contain("plan_probe");
        detail.Should().NotMatchRegex(@"cost|rows|\$1|= 1");
        (await CountAsync("category = 'db.slow_query'")).Should().Be(1);
    }

    [Fact]
    public async Task SameShapeIsPlannedOncePerCooldownAndNonSelectIsNeverPlanned()
    {
        await ResetAsync();
        await using var writer = NewWriter();
        using var planner = new SlowQueryPlanner(writer, _database.DataSource, cooldown: TimeSpan.FromHours(1));
        const string statement = "SELECT id FROM public.plan_probe WHERE id = @id";

        planner.Request("shape-a", statement, null);
        await WaitAsync(() => planner.Planned > 0);
        planner.Request("shape-a", statement, null);
        planner.Request("shape-b", "UPDATE public.plan_probe SET id = @id", null);
        await Task.Delay(300);

        planner.Planned.Should().Be(1);
        planner.Failures.Should().Be(0);
    }

    [Fact]
    public async Task StatementThatCannotBePlannedIsCountedAndNeverBreaksTheCaller()
    {
        await using var writer = NewWriter();
        using var planner = new SlowQueryPlanner(writer, _database.DataSource);

        planner.Request("missing", "SELECT id FROM public.no_such_table WHERE id = @id", null);
        await WaitAsync(() => planner.Failures > 0);

        planner.Failures.Should().Be(1);
        planner.Planned.Should().Be(0);
    }
}
