using ALKAROS.Host.Experience.Observability;
using ALKAROS.Observability.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Observability.Tests;

/// <summary>
/// V1-RMD-326 (independent 2026-09-26 audit, finding K14): proves this is a REAL probe (a real Postgres
/// round trip), not another self-reported claim - a healthy connection records Healthy, and a connection
/// this test deliberately breaks records Unhealthy, both through the exact same
/// <see cref="IObservabilityService.RecordHealthCheckAsync"/> path a caller of the HTTP endpoint would use.
/// </summary>
[Collection("Observability PostgreSQL HTTP")]
public sealed class DatabaseHealthProbeHostedServiceTests : IAsyncLifetime
{
    private readonly ObservabilityTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AProbeAgainstARealHealthyDatabaseRecordsHealthy()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_database.DataSource);
        services.AddObservabilityExperience();
        await using var provider = services.BuildServiceProvider();
        var observability = provider.GetRequiredService<IObservabilityService>();

        var probe = new DatabaseHealthProbeHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            _database.DataSource,
            NullLogger<DatabaseHealthProbeHostedService>.Instance);

        await probe.ProbeAsync(CancellationToken.None);

        var recent = await observability.GetLatestHealthChecksByTargetAsync(
            DatabaseHealthProbeHostedService.Target, limit: 1);
        Assert.Equal(HealthStatus.Healthy, Assert.Single(recent).Status);
    }

    [Fact]
    public async Task AProbeAgainstAnUnreachableDatabaseRecordsUnhealthyInsteadOfThrowing()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_database.DataSource);
        services.AddObservabilityExperience();
        await using var provider = services.BuildServiceProvider();
        var observability = provider.GetRequiredService<IObservabilityService>();

        // A data source pointed at a port nothing listens on - a real, deliberately broken connection, not
        // a mock returning a canned failure.
        await using var brokenDataSource = new NpgsqlDataSourceBuilder(
            "Host=127.0.0.1;Port=1;Username=nobody;Password=nobody;Database=nowhere;Timeout=1").Build();

        var probe = new DatabaseHealthProbeHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            brokenDataSource,
            NullLogger<DatabaseHealthProbeHostedService>.Instance);

        await probe.ProbeAsync(CancellationToken.None);

        var recent = await observability.GetLatestHealthChecksByTargetAsync(
            DatabaseHealthProbeHostedService.Target, limit: 1);
        Assert.Equal(HealthStatus.Unhealthy, Assert.Single(recent).Status);
    }
}
