using ALKAROS.Host.Observability.Wiring;
using ALKAROS.TestHelpers;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Observability.Wiring.Tests;

public sealed class MonitoringDatabase : PgTestDatabase
{
    public MonitoringDatabase() : base("alkaros_rmd551_") { }

    protected override Task ApplySqlAsync() => Task.CompletedTask;
}

public sealed class MonitoringDataSourceTests : IClassFixture<MonitoringDatabase>
{
    private readonly MonitoringDatabase _database;

    public MonitoringDataSourceTests(MonitoringDatabase database) => _database = database;

    [Fact]
    public async Task TheMonitoringPoolIsCappedAtFourConnectionsAndNamedForTheServer()
    {
        Assert.Equal(4, MonitoringDataSource.MaxConnections);
        var baseString = new NpgsqlConnectionStringBuilder(_database.DataSource.ConnectionString) { Timeout = 1 };
        await using var pool = MonitoringDataSource.Create(baseString.ConnectionString);

        var held = new List<NpgsqlConnection>();
        try
        {
            for (var i = 0; i < MonitoringDataSource.MaxConnections; i++)
                held.Add(await pool.OpenConnectionAsync());

            await using var probe = new NpgsqlCommand("SELECT application_name FROM pg_stat_activity WHERE pid = pg_backend_pid()", held[0]);
            Assert.Equal(MonitoringDataSource.ApplicationName, (string?)await probe.ExecuteScalarAsync());

            var exhausted = await Assert.ThrowsAsync<NpgsqlException>(async () => await pool.OpenConnectionAsync());
            Assert.Contains("pool has been exhausted", exhausted.Message);
        }
        finally
        {
            foreach (var connection in held) await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheApplicationPoolIsNotTouchedByTheMonitoringPool()
    {
        await using var pool = MonitoringDataSource.Create(_database.DataSource.ConnectionString);
        await using var monitoring = await pool.OpenConnectionAsync();

        await using var application = await _database.DataSource.OpenConnectionAsync();

        await using var command = new NpgsqlCommand("SELECT application_name FROM pg_stat_activity WHERE pid = pg_backend_pid()", application);
        Assert.NotEqual(MonitoringDataSource.ApplicationName, (string?)await command.ExecuteScalarAsync());
    }
}
