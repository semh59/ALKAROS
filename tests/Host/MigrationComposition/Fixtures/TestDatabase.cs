using ALKAROS.Host.Composition.Migrations;
using Xunit;

namespace ALKAROS.Host.Tests.Fixtures;

/// <summary>
/// Creates and drops a unique PostgreSQL database for a single test against
/// the local PostgreSQL 18 instance. Connection defaults can be overridden
/// with the ALKAROS_TEST_PG_HOST / ALKAROS_TEST_PG_PORT / ALKAROS_TEST_PG_USER
/// / ALKAROS_TEST_PG_PASSWORD environment variables.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    private const string DefaultHost = "localhost";
    private const int DefaultPort = 5432;
    private const string DefaultUser = "postgres";

    public TestDatabase()
    {
        var host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? DefaultHost;
        var port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var parsedPort)
            ? parsedPort
            : DefaultPort;
        var user = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? DefaultUser;
        var password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD");

        Name = "alkaros_fnd004_" + Guid.NewGuid().ToString("N")[..8];
        MaintenanceOptions = new PsqlOptions(
            $"postgresql://{user}@{host}:{port}/postgres", Password: password);
        PsqlOptions = new PsqlOptions(
            $"postgresql://{user}@{host}:{port}/{Name}", Password: password);
    }

    public string Name { get; }

    public string Url => PsqlOptions.DatabaseUrl;

    public PsqlOptions MaintenanceOptions { get; }

    public PsqlOptions PsqlOptions { get; }

    public async Task InitializeAsync()
    {
        var drop = await PsqlScriptRunner.RunCommandAsync(
            $"DROP DATABASE IF EXISTS {Name} WITH (FORCE);", MaintenanceOptions, CancellationToken.None);
        Assert.True(drop.Success, $"Dropping stale test database failed: {drop.ErrorSummary}");

        var create = await PsqlScriptRunner.RunCommandAsync(
            $"CREATE DATABASE {Name};", MaintenanceOptions, CancellationToken.None);
        Assert.True(create.Success, $"CREATE DATABASE failed: {create.ErrorSummary}");
    }

    public async Task DisposeAsync()
    {
        var drop = await PsqlScriptRunner.RunCommandAsync(
            $"DROP DATABASE IF EXISTS {Name} WITH (FORCE);", MaintenanceOptions, CancellationToken.None);
        Assert.True(drop.Success, $"DROP DATABASE failed: {drop.ErrorSummary}");
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        var result = await PsqlScriptRunner.RunCommandAsync(
            $"SELECT to_regclass('public.{tableName}') IS NOT NULL;", PsqlOptions, CancellationToken.None);
        Assert.True(result.Success, $"Control query failed: {result.ErrorSummary}");
        return string.Equals(result.StandardOutput.Trim(), "t", StringComparison.Ordinal);
    }
}
