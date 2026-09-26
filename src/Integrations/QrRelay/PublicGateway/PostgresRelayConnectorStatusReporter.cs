using ALKAROS.QrRelay.LocalConnector;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.QrRelay.PublicGateway;

/// <summary>
/// V12-QRT-005. The `api` container's own view of the relay connector's
/// live status, once the connector moved into its own container and can no
/// longer be read from an in-process <see cref="RelayConnectorSupervisor"/>
/// singleton — reads back what <see cref="RelayConnectorStatusPublisher"/>
/// (running inside the connector container) last wrote. A brief staleness
/// window (up to that publisher's own poll interval) is acceptable for a
/// manager-facing status display; nothing here is a payment/inventory
/// invariant.
/// </summary>
public sealed class PostgresRelayConnectorStatusReporter : IRelayConnectorStatusReporter
{
    private const string ConnectorKey = "cloudflare";
    private static readonly RelayConnectorStatus NotConfigured = new(RelayConnectorState.NotConfigured, null, 0, null);

    // V1-RMD-324 (independent 2026-09-26 audit, finding K17): RelayConnectorStatusPublisher writes this row
    // every 5 seconds while its own container is alive - 30 seconds (6x that interval) tolerates ordinary
    // jitter (a slow write, a GC pause, a brief DB hiccup) without flagging staleness on every normal tick,
    // while still catching a genuinely dead container within one manager-facing status check, not "forever".
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromSeconds(30);

    private readonly NpgsqlDataSource _dataSource;
    private readonly Func<DateTimeOffset> _nowUtc;

    public PostgresRelayConnectorStatusReporter(NpgsqlDataSource dataSource)
        : this(dataSource, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Lets a test control "now" deterministically instead of racing a real 30-second clock.</summary>
    public PostgresRelayConnectorStatusReporter(NpgsqlDataSource dataSource, Func<DateTimeOffset> nowUtc)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _nowUtc = nowUtc ?? throw new ArgumentNullException(nameof(nowUtc));
    }

    // IRelayConnectorStatusReporter.CurrentStatus is a synchronous property
    // (RelayConnectorSupervisor's own in-memory read, V12-QRT-001, out of
    // this task's Owned surface) — genuinely synchronous ADO.NET calls here
    // (not an async call blocked-on-synchronously) so a manager checking
    // /status does one quick round-trip on the calling thread rather than
    // this reporter wrapping async work in a blocking anti-pattern.
    public RelayConnectorStatus CurrentStatus
    {
        get
        {
            using var connection = _dataSource.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT state, last_started_at, restart_count, last_exit_code, updated_at
                FROM qr_ordering.relay_connector_status
                WHERE connector_key = @connector_key;
                """;
            command.Parameters.Add("connector_key", NpgsqlDbType.Text).Value = ConnectorKey;

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return NotConfigured;

            var state = Enum.Parse<RelayConnectorState>(reader.GetString(0));
            var updatedAt = reader.GetFieldValue<DateTimeOffset>(4);

            // V1-RMD-324 (K17): the row's own freshness, not just its content - a "Running" row the
            // publisher stopped updating because its whole container died is no longer trustworthy.
            if (_nowUtc() - updatedAt > StalenessThreshold)
                state = RelayConnectorState.Unknown;

            return new RelayConnectorStatus(
                state,
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3));
        }
    }
}
