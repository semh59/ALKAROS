using System.Diagnostics;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.ServerDetectors;
using ALKAROS.Host.Observability.TraceCorrelation;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Xunit;

namespace ALKAROS.Host.Observability.TraceCorrelation.Tests;

public sealed class TraceDatabase : PgTestDatabase
{
    public TraceDatabase() : base("alkaros_rmd542_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        foreach (var (folder, pattern) in new[] { ("V1-RMD-533", "187-*.up.sql"), ("V1-OPS-001", "015-*.up.sql") })
        {
            var file = Directory.GetFiles(Path.Combine(root, "database", "migrations", "V1", folder), pattern).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public class QuietProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var type = targetMethod!.ReturnType;
        if (type == typeof(Task))
            return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GetGenericArguments()[0]).Invoke(null, [null]);
        return null;
    }
}

public sealed class TraceCorrelationTests : IClassFixture<TraceDatabase>
{
    private const string BrowserTrace = "4bf92f3577b34da6a3ce929d0e0e4736";

    private readonly TraceDatabase _database;

    public TraceCorrelationTests(TraceDatabase database) => _database = database;

    private async Task<WebApplication> StartAppAsync(bool withoutLogging = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        if (withoutLogging)
        {
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.None);
        }

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApiErrorHandling();
        builder.Services.AddSingleton(new MonitoringDataSource(_database.DataSource));
        builder.Services.AddSingleton(DispatchProxy.Create<IAlertService, QuietProxy>());
        builder.Services.AddSingleton<IAuditEventStore>(new PostgresAuditEventStore(_database.DataSource));
        builder.Services.AddServerErrorDetection();
        builder.Services.AddTraceCorrelation();
        var app = builder.Build();
        app.UseTraceCorrelation();
        DualScreenApplication.UseDualScreenErrorHandling(app);
        app.MapGet("/e2e/act", async (HttpContext context, IAuditEventStore audit) =>
        {
            await audit.AppendAsync(new AuditEvent(
                Guid.NewGuid(), "e2e.button", "Button", Guid.NewGuid(), "User", context.TraceIdentifier));
            throw new InvalidOperationException("e2e failure after the audit record");
        });
        app.MapGet("/e2e/queries/{count:int}", async (int count) =>
        {
            for (var i = 0; i < count; i++)
            {
                await using var command = _database.DataSource.CreateCommand($"SELECT {i}");
                await command.ExecuteScalarAsync();
            }

            return Results.Ok();
        });
        app.MapGet("/e2e/ping", () => Results.Ok());
        app.MapGet("/e2e/outgoing", async (HttpContext context) =>
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync($"{context.Request.Scheme}://{context.Request.Host}/e2e/ping");
            return Results.Ok();
        });
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage Call(string path, string? traceparent = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (traceparent is not null)
            request.Headers.Add("traceparent", traceparent);
        return request;
    }

    private static string CorrelationOf(HttpResponseMessage response) => response.Headers.GetValues("X-Correlation-Id").Single();

    [Fact]
    public async Task OneButtonTraceIdIsTheSameInTheResponseTheErrorEventAndTheAuditRecord()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await client.SendAsync(Call("/e2e/act", $"00-{BrowserTrace}-00f067aa0ba902b7-01"));
        await app.Services.GetRequiredService<ErrorEventWriter>().FlushAsync();

        ((int)response.StatusCode).Should().Be(500);
        CorrelationOf(response).Should().Be(BrowserTrace);
        (await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM observability.error_occurrences WHERE trace_id = '{BrowserTrace}';")).Should().BeGreaterThan(0);
        (await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM audit.audit_events WHERE correlation_id = '{BrowserTrace}' AND event_name = 'e2e.button';")).Should().Be(1);
    }

    [Fact]
    public async Task TraceparentIsHonouredEvenWhenTheHostCreatesNoRequestActivityOfItsOwn()
    {
        await using var app = await StartAppAsync(withoutLogging: true);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await client.SendAsync(Call("/e2e/ping", $"00-{BrowserTrace}-00f067aa0ba902b7-01"));

        CorrelationOf(response).Should().Be(BrowserTrace);
    }

    [Fact]
    public async Task RequestWithoutOrWithBrokenTraceparentGetsAFreshTraceId()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var none = await client.SendAsync(Call("/e2e/ping"));
        using var broken = await client.SendAsync(Call("/e2e/ping", "not-a-traceparent"));

        CorrelationOf(none).Should().MatchRegex("^[0-9a-f]{32}$");
        CorrelationOf(broken).Should().MatchRegex("^[0-9a-f]{32}$").And.NotBe(CorrelationOf(none));
    }

    [Fact]
    public async Task DatabaseCommandsBecomeSpansOfTheRequestAndAtMostFiftyAreKept()
    {
        await using var app = await StartAppAsync();
        var buffer = app.Services.GetRequiredService<TraceSpanBuffer>();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var few = await client.SendAsync(Call("/e2e/queries/3"));
        using var many = await client.SendAsync(Call("/e2e/queries/60"));

        var small = buffer.Get(CorrelationOf(few))!;
        small.Spans.Should().HaveCount(3).And.OnlyContain(span => span.Kind == "database");
        small.Route.Should().Be("/e2e/queries/{count:int}");
        var large = buffer.Get(CorrelationOf(many))!;
        large.Spans.Should().HaveCount(TraceSpanBuffer.MaxSpansPerTrace);
        large.DroppedSpans.Should().Be(10);
    }

    [Fact]
    public async Task OutgoingCallsBecomeExternalSpansThatNeverCarryThePath()
    {
        await using var app = await StartAppAsync();
        var buffer = app.Services.GetRequiredService<TraceSpanBuffer>();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        using var response = await client.SendAsync(Call("/e2e/outgoing"));

        var span = buffer.Get(CorrelationOf(response))!.Spans.Should().ContainSingle(s => s.Kind == "external").Subject;
        span.Name.Should().NotContain("/e2e");
        span.DurationMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CommandsOutsideAnyRequestAreNotRecorded()
    {
        await using var app = await StartAppAsync();
        var buffer = app.Services.GetRequiredService<TraceSpanBuffer>();
        var before = buffer.Recent(1000).Count;

        await using var command = _database.DataSource.CreateCommand("SELECT 1");
        await command.ExecuteScalarAsync();

        buffer.Recent(1000).Count.Should().Be(before);
    }
}
