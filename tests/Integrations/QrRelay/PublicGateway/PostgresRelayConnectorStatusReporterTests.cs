using ALKAROS.QrRelay.LocalConnector;
using ALKAROS.QrRelay.PublicGateway.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace ALKAROS.QrRelay.PublicGateway.Tests;

/// <summary>
/// V12-QRT-005: `relay_connector_status` holds a single well-known row,
/// same pattern as `PostgresRelayTunnelStoreTests` — a fresh database per test.
/// </summary>
public sealed class PostgresRelayConnectorStatusReporterTests : IAsyncLifetime
{
    private readonly RelayProviderConfigTestDatabase _database = new();
    private PostgresRelayConnectorStatusReporter _reporter = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _reporter = new PostgresRelayConnectorStatusReporter(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public void WithNothingWrittenYetReportsNotConfigured()
    {
        var status = _reporter.CurrentStatus;

        Assert.Equal(RelayConnectorState.NotConfigured, status.State);
        Assert.Null(status.LastStartedAt);
        Assert.Equal(0, status.RestartCount);
        Assert.Null(status.LastExitCode);
    }

    [Fact]
    public async Task ReadsBackWhateverWasWrittenToTheSingleRow()
    {
        var startedAt = DateTimeOffset.UtcNow;
        await InsertRowAsync("Running", startedAt, restartCount: 3, lastExitCode: null);

        var status = _reporter.CurrentStatus;

        Assert.Equal(RelayConnectorState.Running, status.State);
        // Postgres timestamptz rounds to microsecond precision; .NET's
        // DateTimeOffset carries 100ns ticks, so an exact round-trip
        // comparison is a coin flip on the last digit.
        Assert.Equal(startedAt, status.LastStartedAt!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Equal(3, status.RestartCount);
        Assert.Null(status.LastExitCode);
    }

    [Fact]
    public async Task ReadsBackANonNullLastExitCode()
    {
        await InsertRowAsync("Restarting", DateTimeOffset.UtcNow, restartCount: 1, lastExitCode: 137);

        var status = _reporter.CurrentStatus;

        Assert.Equal(RelayConnectorState.Restarting, status.State);
        Assert.Equal(137, status.LastExitCode);
    }

    private async Task InsertRowAsync(string state, DateTimeOffset startedAt, int restartCount, int? lastExitCode)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.relay_connector_status (
                connector_key, state, last_started_at, restart_count, last_exit_code, updated_at)
            VALUES ('cloudflare', @state, @last_started_at, @restart_count, @last_exit_code, now());
            """);
        command.Parameters.Add("state", NpgsqlDbType.Text).Value = state;
        command.Parameters.Add("last_started_at", NpgsqlDbType.TimestampTz).Value = startedAt;
        command.Parameters.Add("restart_count", NpgsqlDbType.Integer).Value = restartCount;
        command.Parameters.Add("last_exit_code", NpgsqlDbType.Integer).Value = (object?)lastExitCode ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }
}
