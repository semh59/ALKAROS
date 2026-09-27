using ALKAROS.QrRelay.LocalConnector.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace ALKAROS.QrRelay.LocalConnector.Tests;

/// <summary>
/// V12-QRT-005. Real-Postgres tests for RelayConnectorStatusPublisher —
/// proves it actually upserts IRelayConnectorStatusReporter.CurrentStatus
/// into the shared row a real PostgresRelayConnectorStatusReporter (the
/// `api` side, tested separately in the PublicGateway test project) reads
/// back.
/// </summary>
public sealed class RelayConnectorStatusPublisherTests : IAsyncLifetime
{
    private readonly RelayConnectorStatusTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task PublishesTheCurrentStatusOnItsFirstTick()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var reporter = new FakeStatusReporter(
            new RelayConnectorStatus(RelayConnectorState.Running, startedAt, 2, null));
        using var publisher = new RelayConnectorStatusPublisher(
            reporter, _database.DataSource, TimeSpan.FromMilliseconds(20));

        await publisher.StartAsync(CancellationToken.None);
        try
        {
            var row = await WaitForRowAsync();

            Assert.Equal("Running", row.State);
            // Postgres timestamptz rounds to microsecond precision; .NET's
            // DateTimeOffset carries 100ns ticks, so an exact round-trip
            // comparison is a coin flip on the last digit.
            Assert.Equal(startedAt, row.LastStartedAt!.Value, TimeSpan.FromMilliseconds(1));
            Assert.Equal(2, row.RestartCount);
            Assert.Null(row.LastExitCode);
        }
        finally
        {
            await publisher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task LaterTicksOverwriteTheSameRowRatherThanInsertingASecondOne()
    {
        var reporter = new FakeStatusReporter(
            new RelayConnectorStatus(RelayConnectorState.NotConfigured, null, 0, null));
        using var publisher = new RelayConnectorStatusPublisher(
            reporter, _database.DataSource, TimeSpan.FromMilliseconds(20));

        await publisher.StartAsync(CancellationToken.None);
        try
        {
            await WaitForRowAsync();
            reporter.Status = new RelayConnectorStatus(RelayConnectorState.Restarting, DateTimeOffset.UtcNow, 1, 137);

            RelayRow row;
            do
            {
                row = await WaitForRowAsync();
            }
            while (row.State != "Restarting");

            Assert.Equal(1, row.RestartCount);
            Assert.Equal(137, row.LastExitCode);
            Assert.Equal(1, await CountRowsAsync());
        }
        finally
        {
            await publisher.StopAsync(CancellationToken.None);
        }
    }

    private async Task<RelayRow> WaitForRowAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var row = await TryReadRowAsync();
            if (row is not null)
                return row;
            await Task.Delay(20);
        }

        throw new TimeoutException("relay_connector_status row was never written.");
    }

    private async Task<RelayRow?> TryReadRowAsync()
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT state, last_started_at, restart_count, last_exit_code FROM qr_ordering.relay_connector_status;");
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new RelayRow(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3));
    }

    private async Task<long> CountRowsAsync()
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT count(*) FROM qr_ordering.relay_connector_status;");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private sealed record RelayRow(string State, DateTimeOffset? LastStartedAt, int RestartCount, int? LastExitCode);

    private sealed class FakeStatusReporter : IRelayConnectorStatusReporter
    {
        public FakeStatusReporter(RelayConnectorStatus status) => Status = status;

        public RelayConnectorStatus Status { get; set; }

        public Task<RelayConnectorStatus> GetCurrentStatusAsync(CancellationToken cancellationToken) => Task.FromResult(Status);
    }
}
