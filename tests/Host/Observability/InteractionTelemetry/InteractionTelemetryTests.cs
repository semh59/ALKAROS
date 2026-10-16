using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.InteractionTelemetry;
using ALKAROS.Host.Observability.ServerDetectors;
using ALKAROS.Observability.PerformanceMetrics;
using ALKAROS.Observability.TelemetryPrivacy;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.InteractionTelemetry.Tests;

public sealed class InteractionDatabase : PgTestDatabase
{
    public InteractionDatabase() : base("alkaros_rmd541_") { }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    protected override async Task ApplySqlAsync()
    {
        foreach (var (folder, pattern) in new[] { ("V1-RMD-533", "187-*.up.sql"), ("V1-RMD-543", "189-*.up.sql"), ("V1-RMD-541", "191-*.up.sql") })
        {
            var file = Directory.GetFiles(Path.Combine(RepositoryRoot(), "database", "migrations", "V1", folder), pattern).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class InteractionTelemetryTests : IClassFixture<InteractionDatabase>
{
    private readonly InteractionDatabase _database;

    public InteractionTelemetryTests(InteractionDatabase database) => _database = database;

    private static InteractionItem Item(string key = "t:system.save", string kind = "click", int duration = 40, string outcome = "ok", int age = 0) =>
        new(key, kind, age, duration, 0, 0, outcome);

    private Task<int> ClearAsync() => _database.ExecuteAsync("DELETE FROM observability.raw_ui_interactions;");

    private Task<long> CountAsync(string where = "true") =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.raw_ui_interactions WHERE {where};");

    [Fact]
    public async Task ValidItemsAreStoredWithTheServerClockAndInvalidOnesAreDropped()
    {
        await ClearAsync();
        var ingest = new InteractionIngest(_database.DataSource);
        var batch = new InteractionBatch(
        [
            Item(age: 5000),
            Item(key: "Kaydet butonu"),
            Item(key: "ali@example.com"),
            Item(kind: "hover"),
            Item(outcome: "weird"),
            Item(duration: -1),
            Item(duration: 600_001),
            Item(key: new string('a', 65)),
        ]);

        var result = await ingest.StoreAsync(batch, Guid.NewGuid(), CancellationToken.None);

        result.Accepted.Should().Be(1);
        result.Rejected.Should().Be(7);
        (await CountAsync()).Should().Be(1);
        (await CountAsync("role = 'cashier' AND occurred_at < now() - interval '4 seconds' AND occurred_at > now() - interval '8 seconds'")).Should().Be(1);
    }

    [Theory]
    [InlineData("t:managementtext.system.acknowledge", true)]
    [InlineData("auto:button@/cashier/orders/:id", true)]
    [InlineData("save-order", true)]
    [InlineData("Kaydet", false)]
    [InlineData("two words", false)]
    [InlineData("ali@example.com", false)]
    [InlineData("", false)]
    public void OnlyCatalogShapedKeysAreAccepted(string key, bool expected) =>
        InteractionIngest.IsCatalogKey(key).Should().Be(expected);

    [Fact]
    public async Task OnlyTheFirstFiftyItemsOfABatchAreConsidered()
    {
        await ClearAsync();
        var ingest = new InteractionIngest(_database.DataSource);

        var result = await ingest.StoreAsync(new InteractionBatch(Enumerable.Range(0, 80).Select(_ => Item()).ToList()), Guid.NewGuid(), CancellationToken.None);

        result.Accepted.Should().Be(InteractionIngest.MaxItemsPerBatch);
        (await CountAsync()).Should().Be(InteractionIngest.MaxItemsPerBatch);
    }

    [Fact]
    public async Task EachTerminalHasItsOwnWriteAllowancePerMinute()
    {
        await ClearAsync();
        var now = DateTimeOffset.UtcNow;
        var ingest = new InteractionIngest(_database.DataSource, clock: () => now);
        var noisy = Guid.NewGuid();
        var quiet = Guid.NewGuid();
        var fifty = new InteractionBatch(Enumerable.Range(0, 50).Select(_ => Item()).ToList());

        var first = await ingest.StoreAsync(fifty, noisy, CancellationToken.None);
        var second = await ingest.StoreAsync(fifty, noisy, CancellationToken.None);
        var third = await ingest.StoreAsync(fifty, noisy, CancellationToken.None);
        var other = await ingest.StoreAsync(fifty, quiet, CancellationToken.None);

        (first.Accepted + second.Accepted + third.Accepted).Should().Be(InteractionIngest.PerTerminalPerMinute);
        third.RateLimited.Should().BeTrue();
        other.Accepted.Should().Be(50);

        now = now.AddMinutes(2);
        (await ingest.StoreAsync(fifty, noisy, CancellationToken.None)).Accepted.Should().Be(50);
    }

    [Fact]
    public async Task KeysBeyondTheDistinctCapAreFiledUnderOther()
    {
        await ClearAsync();
        var ingest = new InteractionIngest(_database.DataSource);
        for (var start = 0; start < InteractionIngest.MaxDistinctKeys; start += 50)
        {
            var batch = new InteractionBatch(Enumerable.Range(start, 50).Select(i => Item(key: $"cap.k{i}")).ToList());
            await ingest.StoreAsync(batch, Guid.NewGuid(), CancellationToken.None);
        }

        await ingest.StoreAsync(new InteractionBatch([Item(key: "cap.one-too-many"), Item(key: "cap.k1")]), Guid.NewGuid(), CancellationToken.None);

        (await CountAsync("key = 'cap.one-too-many'")).Should().Be(0);
        (await CountAsync($"key = '{InteractionIngest.OtherKey}'")).Should().Be(1);
        (await CountAsync("key = 'cap.k1'")).Should().Be(2);
    }

    [Fact]
    public async Task InteractionsAlsoFeedTheUiInteractionPerformanceMetric()
    {
        await ClearAsync();
        await _database.ExecuteAsync("DELETE FROM observability.perf_minute_summaries;");
        await using var recorder = new PerformanceRecorder(_database.DataSource, TimeSpan.FromHours(1));
        var ingest = new InteractionIngest(_database.DataSource, recorder);

        await ingest.StoreAsync(new InteractionBatch([Item(key: "perf.feed", duration: 120), Item(key: "perf.feed", duration: 80, outcome: "error")]), Guid.NewGuid(), CancellationToken.None);
        await recorder.FlushAsync(includeCurrentMinute: true);

        var rows = await new PerformanceReader(_database.DataSource).QueryAsync(
            PerformanceMetric.UiInteraction, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), "perf.feed");
        var summary = rows.Should().ContainSingle().Subject;
        summary.Count.Should().Be(2);
        summary.Errors.Should().Be(1);
    }

    [Fact]
    public async Task TheRawTableIsUnloggedPrefixedAndInTheThirtyDayRetentionCatalog()
    {
        (await _database.ScalarAsync<string>("SELECT relpersistence::text FROM pg_class WHERE oid = 'observability.raw_ui_interactions'::regclass;")).Should().Be("u");
        TelemetryRetention.IsRawTable("observability.raw_ui_interactions").Should().BeTrue();
        var policy = ErrorRetentionHostedService.Policies.Should().ContainSingle(p => p.Table == "observability.raw_ui_interactions").Subject;
        policy.RetentionDays.Should().Be(30);

        await ClearAsync();
        await _database.ExecuteAsync(
            """
            INSERT INTO observability.raw_ui_interactions (occurred_at, terminal_id, role, key, kind, duration_ms, outcome)
            VALUES (now() - interval '40 days', gen_random_uuid(), 'cashier', 'old', 'click', 1, 'ok'),
                   (now() - interval '2 days', gen_random_uuid(), 'cashier', 'recent', 'click', 1, 'ok');
            """);
        await TelemetryRetention.PurgeAsync(_database.DataSource, [policy], DateTimeOffset.UtcNow);

        (await CountAsync("key = 'old'")).Should().Be(0);
        (await CountAsync("key = 'recent'")).Should().Be(1);
    }

    private static string Run(string file, string arguments, IDictionary<string, string> environment)
    {
        var start = new ProcessStartInfo(file, arguments) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var (name, value) in environment)
            start.Environment[name] = value;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, $"{file} {arguments} failed: {error}");
        return output;
    }

    [Fact]
    public async Task TheBackupScriptKeepsRawRowsOutOfTheDumpButKeepsLoggedTables()
    {
        await ClearAsync();
        await _database.ExecuteAsync(
            """
            INSERT INTO observability.raw_ui_interactions (occurred_at, terminal_id, role, key, kind, duration_ms, outcome)
            SELECT now(), gen_random_uuid(), 'cashier', 'dump.row', 'click', 1, 'ok' FROM generate_series(1, 25);
            """);
        await _database.ExecuteAsync(
            "INSERT INTO observability.error_events (fingerprint, layer, severity, category, first_seen_at, last_seen_at, occurrence_count) VALUES ('dump-fp', 'Api', 'Error', 'api.dump', now(), now(), 1) ON CONFLICT DO NOTHING;");
        var connection = new NpgsqlConnectionStringBuilder(_database.DataSource.ConnectionString);
        var folder = Path.Combine(Path.GetTempPath(), "alkaros-rmd541-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var environment = new Dictionary<string, string>
            {
                ["ALKAROS_DB_HOST"] = connection.Host!,
                ["ALKAROS_DB_PORT"] = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["ALKAROS_DB_USER"] = connection.Username!,
                ["ALKAROS_DB_NAME"] = connection.Database!,
                ["ALKAROS_DB_PASSWORD"] = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD") ?? "postgres",
                ["ALKAROS_BACKUP_DIR"] = folder,
            };
            Run("sh", Path.Combine(InteractionDatabase.RepositoryRoot(), "deploy", "docker", "backup.sh").Replace('\\', '/'), environment);
            var artifact = Directory.GetFiles(folder, "*.dump").Single();

            var contents = Run("pg_restore", $"--list \"{artifact}\"", new Dictionary<string, string>());

            contents.Should().Contain("TABLE observability raw_ui_interactions");
            contents.Should().NotContain("TABLE DATA observability raw_ui_interactions");
            contents.Should().Contain("TABLE DATA observability error_events");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(new DualScreenStore(_database.DataSource));
        builder.Services.AddSingleton(new InteractionIngest(_database.DataSource));
        var app = builder.Build();
        app.MapInteractionTelemetry();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task CallWithoutACashierSessionIsRejectedAndOversizedBodiesAreTooLarge()
    {
        await ClearAsync();
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var anonymous = await client.PostAsJsonAsync("/api/v1/telemetry/interactions", new InteractionBatch([Item()]));
        using var huge = await client.PostAsync(
            "/api/v1/telemetry/interactions",
            new StringContent("{\"items\":[]}" + new string(' ', InteractionTelemetryEndpoints.MaxBodyBytes + 10), System.Text.Encoding.UTF8, "application/json"));

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        huge.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await CountAsync()).Should().Be(0);
    }
}
