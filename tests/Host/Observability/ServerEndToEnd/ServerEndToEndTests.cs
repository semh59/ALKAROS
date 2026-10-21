using System.Reflection;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Observability.ServerDetectors;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.ServerEndToEnd.Tests;

public sealed class EndToEndDatabase : PgTestDatabase
{
    public EndToEndDatabase() : base("alkaros_rmd560_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        // The daily retention service purges these tables at start-up, so they must exist or it reports its own failure.
        foreach (var (task, prefix) in new[]
        {
            ("V1-OBS-001", "028"), ("V1-OPS-002", "029"), ("V1-RMD-533", "187"), ("V1-RMD-538", "188"),
            ("V1-RMD-541", "191"), ("V1-RMD-568", "192"),
        })
        {
            var file = Directory.GetFiles(
                Path.Combine(root, "database", "migrations", "V1", task), prefix + "-*.up.sql").Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

/// <summary>Answers every call of a collaborator interface with a completed task; the real services are not under test.</summary>
public class CompletedTaskProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var type = targetMethod!.ReturnType;
        if (type == typeof(Task))
            return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var fromResult = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GetGenericArguments()[0]);
            return fromResult.Invoke(null, [null]);
        }

        return null;
    }
}

public sealed class ServerEndToEndTests : IClassFixture<EndToEndDatabase>
{
    private readonly EndToEndDatabase _database;

    public ServerEndToEndTests(EndToEndDatabase database) => _database = database;

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApiErrorHandling();
        builder.Services.AddSingleton(new MonitoringDataSource(_database.DataSource));
        builder.Services.AddSingleton(DispatchProxy.Create<IAlertService, CompletedTaskProxy>());
        builder.Services.AddSingleton(DispatchProxy.Create<IAuditEventStore, CompletedTaskProxy>());
        builder.Services.AddServerErrorDetection();
        var app = builder.Build();
        DualScreenApplication.UseDualScreenErrorHandling(app);
        app.MapGet("/e2e/boom", (HttpContext _) => Task.FromException(new InvalidOperationException("e2e boom")));
        app.MapGet("/e2e/missing", (HttpContext _) => Task.FromException(new DualScreenNotFoundException("e2e missing")));
        await app.StartAsync();
        return app;
    }

    private async Task<long> OccurrencesAsync(string category) =>
        await _database.ScalarAsync<long>(
            $"SELECT COALESCE(sum(occurrence_count), 0)::bigint FROM observability.error_events WHERE category = '{category}';");

    [Fact]
    public async Task RealServerErrorIsRecordedOnceAndClientStillGetsTheStandardEnvelope()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        var before = await OccurrencesAsync("api.unhandled_exception");

        using var response = await client.GetAsync("/e2e/boom");
        await app.Services.GetRequiredService<ErrorEventWriter>().FlushAsync();

        ((int)response.StatusCode).Should().Be(500);
        (await response.Content.ReadAsStringAsync()).Should().Contain("error");
        (await OccurrencesAsync("api.unhandled_exception")).Should().Be(before + 1);
    }

    [Fact]
    public async Task RealClientErrorProducesNoEvent()
    {
        await using var app = await StartAppAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        var before = await _database.ScalarAsync<long>(
            "SELECT COALESCE(sum(occurrence_count), 0)::bigint FROM observability.error_events;");

        using var response = await client.GetAsync("/e2e/missing");
        await app.Services.GetRequiredService<ErrorEventWriter>().FlushAsync();

        ((int)response.StatusCode).Should().Be(404);
        (await _database.ScalarAsync<long>(
            "SELECT COALESCE(sum(occurrence_count), 0)::bigint FROM observability.error_events;")).Should().Be(before);
    }
}
