using System.Diagnostics;
using System.Net;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Observability.ErrorEventEndpoints;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.ErrorEventEndpoints.Tests;

public sealed class EndpointDatabase : PgTestDatabase
{
    public EndpointDatabase() : base("alkaros_rmd539_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var file = Directory.GetFiles(
            Path.Combine(root, "database", "migrations", "V1", "V1-RMD-533"), "187-*.up.sql").Single();
        await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}

public sealed class ErrorEventEndpointsTests : IClassFixture<EndpointDatabase>
{
    private readonly EndpointDatabase _database;

    public ErrorEventEndpointsTests(EndpointDatabase database) => _database = database;

    private Task<int> SeedAsync(string fingerprint, string layer, string severity, string status = "Open", int minutesAgo = 0) =>
        _database.ExecuteAsync(
            """
            INSERT INTO observability.error_events
                (fingerprint, layer, severity, category, first_seen_at, last_seen_at, occurrence_count, status)
            VALUES (@f, @layer, @sev, 'api.test', now() - interval '1 day', now() - make_interval(mins => @ago), 3, @status);
            """,
            ("f", fingerprint), ("layer", layer), ("sev", severity), ("status", status), ("ago", minutesAgo));

    [Fact]
    public async Task ListFiltersByLayerSeverityAndStatusAndPages()
    {
        var queries = new ErrorEventQueries(_database.DataSource);
        await SeedAsync("l-1", "Printer", "Critical", minutesAgo: 1);
        await SeedAsync("l-2", "Printer", "Warning", minutesAgo: 2);
        await SeedAsync("l-3", "Api", "Critical", "Resolved", 3);

        (await queries.ListAsync("Printer", null, null, 1, 50, CancellationToken.None)).Items
            .Select(i => i.Fingerprint).Should().Equal("l-1", "l-2");
        (await queries.ListAsync("Printer", "Critical", null, 1, 50, CancellationToken.None)).Items
            .Should().ContainSingle().Which.Fingerprint.Should().Be("l-1");
        (await queries.ListAsync(null, null, "Resolved", 1, 100, CancellationToken.None)).Items
            .Select(i => i.Fingerprint).Should().Contain("l-3").And.NotContain(["l-1", "l-2"]);
        var page = await queries.ListAsync("Printer", null, null, 2, 1, CancellationToken.None);
        page.Items.Should().ContainSingle().Which.Fingerprint.Should().Be("l-2");
        page.Total.Should().Be(2);
        (await queries.ListAsync(null, null, null, 1, 100000, CancellationToken.None)).PageSize.Should().Be(ErrorEventQueries.MaxPageSize);
    }

    [Fact]
    public async Task UnknownFilterValuesAreIgnoredNotInjected()
    {
        var queries = new ErrorEventQueries(_database.DataSource);
        await SeedAsync("inj-1", "Backup", "Error");

        var page = await queries.ListAsync("x'; DELETE FROM observability.error_events; --", null, null, 1, 100, CancellationToken.None);

        page.Total.Should().BeGreaterThan(0);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events;")).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AcknowledgeAndResolveChangeStatusOnceAndReportUnknown()
    {
        var queries = new ErrorEventQueries(_database.DataSource);
        await SeedAsync("st-1", "Security", "Error");

        (await queries.AcknowledgeAsync("st-1", Guid.NewGuid(), CancellationToken.None)).Should().BeTrue();
        (await queries.AcknowledgeAsync("st-1", Guid.NewGuid(), CancellationToken.None)).Should().BeFalse();
        (await queries.ResolveAsync("st-1", CancellationToken.None)).Should().BeTrue();
        (await queries.ResolveAsync("st-1", CancellationToken.None)).Should().BeFalse();
        (await queries.ResolveAsync("nope", CancellationToken.None)).Should().BeFalse();
        (await queries.GetAsync("st-1", CancellationToken.None))!.Event.Status.Should().Be("Resolved");
        (await queries.GetAsync("nope", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task FirstPageOfTenThousandEventsIsUnderThreeHundredMilliseconds()
    {
        await _database.ExecuteAsync(
            """
            INSERT INTO observability.error_events
                (fingerprint, layer, severity, category, first_seen_at, last_seen_at, occurrence_count)
            SELECT 'bulk-' || g, 'Api', 'Warning', 'api.bulk', now(), now() - make_interval(secs => g), 1
            FROM generate_series(1, 10000) g;
            """);
        await _database.ExecuteAsync("ANALYZE observability.error_events;");
        var queries = new ErrorEventQueries(_database.DataSource);
        await queries.ListAsync(null, null, null, 1, 50, CancellationToken.None);

        var watch = Stopwatch.StartNew();
        var page = await queries.ListAsync(null, null, "Open", 1, 50, CancellationToken.None);
        watch.Stop();

        page.Items.Should().HaveCount(50);
        page.Total.Should().BeGreaterThanOrEqualTo(10000);
        watch.ElapsedMilliseconds.Should().BeLessThan(300);
    }

    [Fact]
    public async Task CallWithoutAManagerSessionIsRejected()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddObservabilityExperience();
        builder.Services.AddSingleton(new ErrorEventQueries(_database.DataSource));
        await using var app = builder.Build();
        app.MapErrorEventEndpoints();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await client.GetAsync("/api/v1/management/observability/errors/");

        ((int)response.StatusCode).Should().BeOneOf(401, 403);
    }
}
