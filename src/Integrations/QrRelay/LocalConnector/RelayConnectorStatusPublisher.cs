using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// V12-QRT-005. Periodically writes <see cref="RelayConnectorSupervisor"/>'s
/// own live status (via <see cref="IRelayConnectorStatusReporter"/>, unchanged)
/// to Postgres, so the `api` container — no longer in the same process once
/// the connector runs in its own container — can still show it on the
/// RelaySettings status endpoint. A separate <see cref="BackgroundService"/>
/// rather than a change to <see cref="RelayConnectorSupervisor"/> itself: that
/// class's own responsibility (supervise `cloudflared`) does not change.
/// </summary>
public sealed class RelayConnectorStatusPublisher : BackgroundService
{
    private const string ConnectorKey = "cloudflare";
    private static readonly TimeSpan DefaultPublishInterval = TimeSpan.FromSeconds(5);

    private readonly IRelayConnectorStatusReporter _statusReporter;
    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeSpan _publishInterval;

    public RelayConnectorStatusPublisher(IRelayConnectorStatusReporter statusReporter, NpgsqlDataSource dataSource)
        : this(statusReporter, dataSource, DefaultPublishInterval)
    {
    }

    /// <summary>Lets a test override the production 5s default (same pattern as RelayConnectorSupervisor's own constructor).</summary>
    public RelayConnectorStatusPublisher(
        IRelayConnectorStatusReporter statusReporter, NpgsqlDataSource dataSource, TimeSpan publishInterval)
    {
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _publishInterval = publishInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (NpgsqlException)
            {
                // A transient DB hiccup must not crash this publisher — the
                // next tick tries again; the connector itself keeps running
                // regardless (RelayConnectorSupervisor has no dependency on
                // this class).
            }

            try
            {
                await Task.Delay(_publishInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        var status = await _statusReporter.GetCurrentStatusAsync(cancellationToken);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO qr_ordering.relay_connector_status (
                connector_key, state, last_started_at, restart_count, last_exit_code, updated_at)
            VALUES (@connector_key, @state, @last_started_at, @restart_count, @last_exit_code, @updated_at)
            ON CONFLICT (connector_key) DO UPDATE
                SET state = EXCLUDED.state,
                    last_started_at = EXCLUDED.last_started_at,
                    restart_count = EXCLUDED.restart_count,
                    last_exit_code = EXCLUDED.last_exit_code,
                    updated_at = EXCLUDED.updated_at;
            """);
        command.Parameters.Add("connector_key", NpgsqlDbType.Text).Value = ConnectorKey;
        command.Parameters.Add("state", NpgsqlDbType.Text).Value = status.State.ToString();
        command.Parameters.Add("last_started_at", NpgsqlDbType.TimestampTz).Value =
            (object?)status.LastStartedAt ?? DBNull.Value;
        command.Parameters.Add("restart_count", NpgsqlDbType.Integer).Value = status.RestartCount;
        command.Parameters.Add("last_exit_code", NpgsqlDbType.Integer).Value =
            (object?)status.LastExitCode ?? DBNull.Value;
        command.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
