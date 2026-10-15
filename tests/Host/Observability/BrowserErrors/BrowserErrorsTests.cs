using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.BrowserErrors;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.BrowserErrors.Tests;

public sealed class BrowserDatabase : PgTestDatabase
{
    public BrowserDatabase() : base("alkaros_rmd537_") { }

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

public sealed class BrowserErrorsTests : IClassFixture<BrowserDatabase>
{
    private readonly BrowserDatabase _database;

    public BrowserErrorsTests(BrowserDatabase database) => _database = database;

    private ErrorEventWriter NewWriter() => new(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

    private static BrowserErrorReport Report(string id, string kind = "js_error", string route = "/cashier/") =>
        new(id, kind, route);

    [Fact]
    public async Task ReportWithoutASessionIsRejectedWith401()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var writer = NewWriter();
        builder.Services.AddSingleton(new DualScreenStore(_database.DataSource));
        builder.Services.AddSingleton(new BrowserErrorIngest(writer));
        await using var app = builder.Build();
        app.MapBrowserErrors();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await client.PostAsJsonAsync("/api/v1/telemetry/browser-errors", Report("e1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SameEventIdIsAcceptedOnceAndKnownKindBecomesACatalogCategory()
    {
        await using var writer = NewWriter();
        var ingest = new BrowserErrorIngest(writer);
        var session = Guid.NewGuid();

        ingest.Accept(Report("dup-1", "unhandled_rejection"), session, Guid.NewGuid()).Should().Be(BrowserErrorVerdict.Accepted);
        ingest.Accept(Report("dup-1", "unhandled_rejection"), session, Guid.NewGuid()).Should().Be(BrowserErrorVerdict.Duplicate);
        await writer.FlushAsync();

        (await _database.ScalarAsync<long>(
            "SELECT count(*) FROM observability.error_events WHERE category = 'browser.unhandled_rejection' AND layer = 'Browser';"))
            .Should().Be(1);
    }

    [Fact]
    public async Task UnknownKindFallsBackToAFixedCategoryAndNeverStoresClientText()
    {
        await using var writer = NewWriter();
        var ingest = new BrowserErrorIngest(writer);

        ingest.Accept(Report("unk-1", "ali@example.com wrote this"), Guid.NewGuid(), Guid.NewGuid());
        await writer.FlushAsync();

        (await _database.ScalarAsync<long>(
            "SELECT count(*) FROM observability.error_events WHERE category = 'browser.unknown';")).Should().BeGreaterThan(0);
        (await _database.ScalarAsync<long>(
            "SELECT count(*) FROM observability.error_events WHERE detail LIKE '%ali@%' OR category LIKE '%ali@%';")).Should().Be(0);
    }

    [Fact]
    public async Task SessionQuotaIsolatesANoisyPageFromOtherSessions()
    {
        await using var writer = NewWriter();
        var ingest = new BrowserErrorIngest(writer);
        var noisy = Guid.NewGuid();
        var verdicts = Enumerable.Range(0, BrowserErrorIngest.PerSessionPerMinute + 5)
            .Select(i => ingest.Accept(Report($"noisy-{i}"), noisy, Guid.NewGuid()))
            .ToList();

        verdicts.Count(v => v == BrowserErrorVerdict.RateLimited).Should().Be(5);
        ingest.Accept(Report("other-1"), Guid.NewGuid(), Guid.NewGuid()).Should().Be(BrowserErrorVerdict.Accepted);
    }

    [Theory]
    [InlineData("/orders/3f2c1b0a-1111-2222-3333-444455556666/lines/42?token=abc#x", "/orders/:id/lines/:n")]
    [InlineData("", "unknown")]
    public void RouteIsReducedToATemplate(string route, string expected) =>
        BrowserErrorIngest.RouteTemplate(route).Should().Be(expected);

    [Fact]
    public async Task EmptyEventIdIsInvalid()
    {
        await using var writer = NewWriter();

        new BrowserErrorIngest(writer).Accept(Report(""), Guid.NewGuid(), Guid.NewGuid())
            .Should().Be(BrowserErrorVerdict.Invalid);
    }
}
