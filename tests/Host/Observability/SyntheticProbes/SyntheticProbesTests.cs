using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.SyntheticProbes.Tests;

public sealed class ProbeDatabase : PgTestDatabase
{
    public ProbeDatabase() : base("alkaros_rmd545_") { }

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

public sealed class SyntheticProbesTests : IClassFixture<ProbeDatabase>
{
    private readonly ProbeDatabase _database;

    public SyntheticProbesTests(ProbeDatabase database) => _database = database;

    private ErrorEventWriter NewWriter() => new(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

    private Task<long> CountAsync(string category) =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{category}';");

    [Fact]
    public async Task FailingProbeRaisesAnErrorEventFlaggedSynthetic()
    {
        await using var writer = NewWriter();
        var runner = new SyntheticProbeRunner(
            [new SyntheticProbe("catalog", _ => throw new InvalidOperationException("down"))], writer);

        var results = await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        results.Should().ContainSingle().Which.Succeeded.Should().BeFalse();
        (await CountAsync("canary.catalog_failed")).Should().Be(1);
        (await _database.ScalarAsync<bool>(
            "SELECT synthetic FROM observability.error_events WHERE category = 'canary.catalog_failed';")).Should().BeTrue();
    }

    [Fact]
    public async Task SlowProbeIsAWarningAndFastProbeIsSilent()
    {
        await using var writer = NewWriter();
        var runner = new SyntheticProbeRunner(
            [
                new SyntheticProbe("slowprobe", token => Task.Delay(150, token)),
                new SyntheticProbe("fastprobe", _ => Task.CompletedTask),
            ],
            writer,
            slowThreshold: TimeSpan.FromMilliseconds(50));

        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        (await CountAsync("canary.slowprobe_slow")).Should().Be(1);
        (await CountAsync("canary.fastprobe_slow")).Should().Be(0);
        (await CountAsync("canary.fastprobe_failed")).Should().Be(0);
    }

    [Fact]
    public async Task RegisteredDatabaseProbeSucceedsAgainstARealDatabaseAndWritesNothing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(new MonitoringDataSource(_database.DataSource));
        await using var writer = NewWriter();
        services.AddSingleton(writer);
        services.AddSyntheticProbes();
        await using var provider = services.BuildServiceProvider();
        var before = await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events;");

        var results = await provider.GetRequiredService<SyntheticProbeRunner>().RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();

        results.Should().ContainSingle(r => r.Name == "database" && r.Succeeded);
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events;")).Should().Be(before);
    }

    [Fact]
    public async Task HealthProbeIsOnlyRegisteredWhenABaseUrlIsConfigured()
    {
        var configured = new ServiceCollection();
        configured.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [SyntheticProbesExtensions.BaseUrlSetting] = "http://127.0.0.1:1" }).Build());
        configured.AddSingleton(new MonitoringDataSource(_database.DataSource));
        await using var writer = NewWriter();
        configured.AddSingleton(writer);
        configured.AddSyntheticProbes();
        await using var provider = configured.BuildServiceProvider();

        var results = await provider.GetRequiredService<SyntheticProbeRunner>().RunOnceAsync(CancellationToken.None);

        results.Select(r => r.Name).Should().BeEquivalentTo("database", "health");
        results.Single(r => r.Name == "health").Succeeded.Should().BeFalse();
    }
}
