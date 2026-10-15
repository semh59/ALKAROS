using System.Security.Claims;
using ALKAROS.Host.Observability.HealthSnapshot;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.ResourceProbes.Tests;

public sealed class ResourceDatabase : PgTestDatabase
{
    public ResourceDatabase() : base("alkaros_rmd547_") { }

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

public sealed class ResourceProbesTests : IClassFixture<ResourceDatabase>
{
    private readonly ResourceDatabase _database;

    public ResourceProbesTests(ResourceDatabase database) => _database = database;

    private Task<long> CountAsync(string category) =>
        _database.ScalarAsync<long>($"SELECT count(*) FROM observability.error_events WHERE category = '{category}';");

    private static ResourceMonitor Monitor(ErrorEventWriter writer, DiskUsage disk, long workingSet = 10, long available = 100) =>
        new(writer, () => disk, () => (workingSet, available));

    [Fact]
    public async Task DiskAtNinetyPercentRaisesWarningAndBelowRaisesNothing()
    {
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

        Monitor(writer, new DiskUsage(11, 100)).CheckOnce().Should().Be(0);
        Monitor(writer, new DiskUsage(10, 100)).CheckOnce().Should().Be(1);
        await writer.FlushAsync();

        (await CountAsync("resource.disk_high")).Should().Be(1);
        (await _database.ScalarAsync<string>(
            "SELECT severity FROM observability.error_events WHERE category = 'resource.disk_high';")).Should().Be("Warning");
    }

    [Fact]
    public async Task DiskAtNinetyFivePercentIsCritical()
    {
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        Monitor(writer, new DiskUsage(5, 100)).CheckOnce().Should().Be(1);
        await writer.FlushAsync();

        (await _database.ScalarAsync<string>(
            "SELECT severity FROM observability.error_events WHERE category = 'resource.disk_high' ORDER BY last_seen_at DESC LIMIT 1;"))
            .Should().Be("Critical");
    }

    [Fact]
    public async Task MemoryNearTheLimitRaisesAnEvent()
    {
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));

        Monitor(writer, new DiskUsage(50, 100), workingSet: 95, available: 100).CheckOnce().Should().Be(1);
        await writer.FlushAsync();

        (await CountAsync("resource.memory_high")).Should().Be(1);
    }

    private sealed class FakeCallerContext : HubCallerContext
    {
        public override string ConnectionId => "c1";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort()
        {
        }
    }

    private sealed class EmptyHub : Hub;

    [Fact]
    public async Task OnlyErrorDisconnectsAreCountedAndTheDisconnectAlwaysContinues()
    {
        var counter = new ConnectionDropCounter();
        var filter = new ConnectionDropFilter(counter);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new HubLifetimeContext(new FakeCallerContext(), provider, new EmptyHub());
        var continued = 0;

        await filter.OnDisconnectedAsync(context, null, (_, _) => { continued++; return Task.CompletedTask; });
        counter.Drops.Should().Be(0);

        await filter.OnDisconnectedAsync(
            context, new ConnectionAbortedException("lost"), (_, _) => { continued++; return Task.CompletedTask; });
        counter.Drops.Should().Be(1);
        continued.Should().Be(2);
    }

    [Fact]
    public async Task ProbeFailureIsCountedAndDoesNotStopTheService()
    {
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        var monitor = new ResourceMonitor(writer, () => throw new IOException("drive gone"), () => (1, 100));
        using var service = new ResourceMonitorHostedService(monitor, TimeSpan.FromMilliseconds(20));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await service.StopAsync(CancellationToken.None);

        service.Failures.Should().BeGreaterThan(1);
    }
}
