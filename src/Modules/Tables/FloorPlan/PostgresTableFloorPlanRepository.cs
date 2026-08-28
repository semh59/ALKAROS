using System.Data;
using ALKAROS.Tables.TableLifecycle;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.FloorPlan;

public sealed class PostgresTableFloorPlanRepository : ITableFloorPlanRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableFloorPlanRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<FloorPlanSnapshot?> GetAsync(
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        if (zoneId == Guid.Empty)
            throw new FloorPlanValidationException("Zone ID cannot be empty.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var snapshot = await ReadSnapshotAsync(connection, transaction, zoneId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return snapshot;
    }

    public async Task<FloorPlanSaveResult> SaveAsync(
        SaveFloorPlan request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        await RequireZoneAsync(connection, transaction, request.ZoneId, cancellationToken);
        var currentFloorVersion = await GetFloorVersionAsync(connection, transaction, request.ZoneId, cancellationToken);
        RequireVersion("floor plan", request.ZoneId, request.ExpectedRowVersion, currentFloorVersion);

        var tables = await GetTablesAsync(connection, transaction, request.ZoneId, cancellationToken);
        foreach (var table in request.Tables)
        {
            if (tables.TryGetValue(table.TableId, out var current))
                RequireVersion("table", table.TableId, table.ExpectedTableRowVersion, current.RowVersion);
        }
        var overlapPairs = await GetMergeOverlapPairsAsync(connection, transaction, request.ZoneId, cancellationToken);
        var warnings = FloorPlanValidator.Validate(
            request,
            tables.ToDictionary(pair => pair.Key, pair => pair.Value.Capacity),
            (left, right) => overlapPairs.Contains(CanonicalPair(left, right)));

        var currentLayouts = await GetLayoutVersionsAsync(
            connection,
            transaction,
            request.Tables.Select(table => table.TableId).ToArray(),
            cancellationToken);
        foreach (var table in request.Tables)
        {
            currentLayouts.TryGetValue(table.TableId, out var actual);
            RequireVersion("table layout", table.TableId, table.ExpectedLayoutRowVersion, actual == 0 ? null : actual);
        }

        var tableIds = request.Tables.Select(table => table.TableId).ToArray();
        var requestedSeats = request.Tables.SelectMany(table => table.Seats.Select(seat => (table.TableId, Seat: seat))).ToList();
        var currentSeats = await GetSeatVersionsAsync(
            connection,
            transaction,
            request.ZoneId,
            requestedSeats.Select(item => item.Seat.SeatId).ToArray(),
            cancellationToken);
        foreach (var (tableId, seat) in requestedSeats)
        {
            if (currentSeats.TryGetValue(seat.SeatId, out var current))
            {
                if (current.TableId != tableId)
                    throw new FloorPlanValidationException($"Seat {seat.SeatId} belongs to a different table.");
                RequireVersion("seat", seat.SeatId, seat.ExpectedRowVersion, current.RowVersion);
            }
            else
            {
                RequireVersion("seat", seat.SeatId, seat.ExpectedRowVersion, null);
            }
        }

        await UpsertFloorPlanAsync(connection, transaction, request, currentFloorVersion, cancellationToken);
        await UpsertLayoutsAsync(connection, transaction, request, currentLayouts, cancellationToken);
        await DeleteRemovedSeatsAsync(
            connection,
            transaction,
            tableIds,
            requestedSeats.Select(item => item.Seat.SeatId).ToArray(),
            cancellationToken);
        await UpsertSeatsAsync(connection, transaction, requestedSeats, currentSeats, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await GetAsync(request.ZoneId, cancellationToken)
            ?? throw new InvalidOperationException("Saved floor plan could not be read back.");
        return new FloorPlanSaveResult(snapshot, warnings);
    }

    private static async Task RequireZoneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        if (zoneId == Guid.Empty)
            throw new FloorPlanValidationException("Zone ID cannot be empty.");

        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM table_mgmt.zones WHERE zone_id = @zone_id FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.AddWithValue("zone_id", zoneId);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new FloorPlanNotFoundException($"Zone {zoneId} was not found.");
    }

    private static async Task<long?> GetFloorVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT row_version FROM table_mgmt.zone_floor_plans WHERE zone_id = @zone_id FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.AddWithValue("zone_id", zoneId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : (long)value;
    }

    private static async Task<Dictionary<Guid, (int Capacity, long RowVersion)>> GetTablesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, (int Capacity, long RowVersion)>();
        await using var command = new NpgsqlCommand(
            "SELECT table_id, capacity, row_version FROM table_mgmt.tables WHERE zone_id = @zone_id ORDER BY table_id FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.AddWithValue("zone_id", zoneId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0), (reader.GetInt32(1), reader.GetInt64(2)));
        return result;
    }

    private static async Task<HashSet<(Guid Left, Guid Right)>> GetMergeOverlapPairsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        var groups = new Dictionary<Guid, HashSet<Guid>>();
        await using var command = new NpgsqlCommand(
            """
            SELECT m.merge_group_id, m.primary_table_id, m.merged_table_id
            FROM table_mgmt.table_merges m
            JOIN table_mgmt.tables p ON p.table_id = m.primary_table_id
            JOIN table_mgmt.tables x ON x.table_id = m.merged_table_id
            WHERE m.status = 'Active' AND p.zone_id = @zone_id AND x.zone_id = @zone_id
            ORDER BY m.merge_group_id, m.table_merge_id
            FOR SHARE OF m;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("zone_id", zoneId);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var groupId = reader.GetGuid(0);
                if (!groups.TryGetValue(groupId, out var tableIds))
                {
                    tableIds = [];
                    groups.Add(groupId, tableIds);
                }
                tableIds.Add(reader.GetGuid(1));
                tableIds.Add(reader.GetGuid(2));
            }
        }

        var result = new HashSet<(Guid Left, Guid Right)>();
        foreach (var tableIds in groups.Values)
        {
            var ordered = tableIds.Order().ToArray();
            for (var left = 0; left < ordered.Length; left++)
            {
                for (var right = left + 1; right < ordered.Length; right++)
                    result.Add((ordered[left], ordered[right]));
            }
        }
        return result;
    }

    private static async Task<Dictionary<Guid, long>> GetLayoutVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] tableIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, long>();
        await using var command = new NpgsqlCommand(
            "SELECT table_id, row_version FROM table_mgmt.table_layouts WHERE table_id = ANY(@table_ids) FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.Add("table_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = tableIds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0), reader.GetInt64(1));
        return result;
    }

    private static async Task<Dictionary<Guid, (Guid TableId, long RowVersion)>> GetSeatVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        Guid[] requestedSeatIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, (Guid TableId, long RowVersion)>();
        await using var command = new NpgsqlCommand(
            """
            SELECT s.seat_id, s.table_id, s.row_version
            FROM table_mgmt.table_seats s
            JOIN table_mgmt.tables t ON t.table_id = s.table_id
            WHERE t.zone_id = @zone_id OR s.seat_id = ANY(@seat_ids)
            FOR UPDATE OF s;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("zone_id", zoneId);
        command.Parameters.Add("seat_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = requestedSeatIds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0), (reader.GetGuid(1), reader.GetInt64(2)));
        return result;
    }

    private static async Task UpsertFloorPlanAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SaveFloorPlan request,
        long? currentVersion,
        CancellationToken cancellationToken)
    {
        var sql = currentVersion is null
            ? """
              INSERT INTO table_mgmt.zone_floor_plans (zone_id, canvas_width, canvas_height, row_version)
              VALUES (@zone_id, @canvas_width, @canvas_height, 1);
              """
            : """
              UPDATE table_mgmt.zone_floor_plans
              SET canvas_width = @canvas_width,
                  canvas_height = @canvas_height,
                  row_version = row_version + 1
              WHERE zone_id = @zone_id;
              """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("zone_id", request.ZoneId);
        command.Parameters.AddWithValue("canvas_width", request.CanvasWidth);
        command.Parameters.AddWithValue("canvas_height", request.CanvasHeight);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertLayoutsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SaveFloorPlan request,
        Dictionary<Guid, long> currentLayouts,
        CancellationToken cancellationToken)
    {
        foreach (var table in request.Tables)
        {
            var exists = currentLayouts.ContainsKey(table.TableId);
            var sql = exists
                ? """
                  UPDATE table_mgmt.table_layouts
                  SET zone_id = @zone_id, x = @x, y = @y, width = @width, height = @height,
                      shape = @shape, rotation_degrees = @rotation_degrees, row_version = row_version + 1
                  WHERE table_id = @table_id;
                  """
                : """
                  INSERT INTO table_mgmt.table_layouts
                      (table_id, zone_id, x, y, width, height, shape, rotation_degrees, row_version)
                  VALUES
                      (@table_id, @zone_id, @x, @y, @width, @height, @shape, @rotation_degrees, 1);
                  """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("table_id", table.TableId);
            command.Parameters.AddWithValue("zone_id", request.ZoneId);
            command.Parameters.AddWithValue("x", table.X);
            command.Parameters.AddWithValue("y", table.Y);
            command.Parameters.AddWithValue("width", table.Width);
            command.Parameters.AddWithValue("height", table.Height);
            command.Parameters.AddWithValue("shape", table.Shape.ToString());
            command.Parameters.AddWithValue("rotation_degrees", table.RotationDegrees);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task DeleteRemovedSeatsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] tableIds,
        Guid[] requestedSeatIds,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "DELETE FROM table_mgmt.table_seats WHERE table_id = ANY(@table_ids) AND NOT (seat_id = ANY(@seat_ids));",
            connection,
            transaction);
        command.Parameters.Add("table_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = tableIds;
        command.Parameters.Add("seat_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = requestedSeatIds;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertSeatsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<(Guid TableId, SaveFloorSeat Seat)> requestedSeats,
        Dictionary<Guid, (Guid TableId, long RowVersion)> currentSeats,
        CancellationToken cancellationToken)
    {
        foreach (var (tableId, seat) in requestedSeats)
        {
            var exists = currentSeats.ContainsKey(seat.SeatId);
            var sql = exists
                ? """
                  UPDATE table_mgmt.table_seats
                  SET table_id = @table_id, seat_number = @seat_number, label = @label,
                      x = @x, y = @y, row_version = row_version + 1
                  WHERE seat_id = @seat_id;
                  """
                : """
                  INSERT INTO table_mgmt.table_seats
                      (seat_id, table_id, seat_number, label, x, y, row_version)
                  VALUES
                      (@seat_id, @table_id, @seat_number, @label, @x, @y, 1);
                  """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("seat_id", seat.SeatId);
            command.Parameters.AddWithValue("table_id", tableId);
            command.Parameters.AddWithValue("seat_number", seat.Number);
            command.Parameters.AddWithValue("label", seat.Label.Trim());
            command.Parameters.AddWithValue("x", seat.X);
            command.Parameters.AddWithValue("y", seat.Y);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void RequireVersion(string resource, Guid id, long expected, long? actual)
    {
        if (expected < 0)
            throw new FloorPlanValidationException($"Expected {resource} row version cannot be negative.");
        if ((actual is null && expected != 0) || (actual is not null && expected != actual.Value))
            throw new FloorPlanConcurrencyException(resource, id, expected, actual);
    }

    private static (Guid Left, Guid Right) CanonicalPair(Guid left, Guid right)
        => left.CompareTo(right) < 0 ? (left, right) : (right, left);

    private static async Task<FloorPlanSnapshot?> ReadSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        Guid readZoneId;
        string zoneCode;
        string zoneName;
        int canvasWidth;
        int canvasHeight;
        long rowVersion;

        await using (var command = new NpgsqlCommand(
            """
            SELECT z.zone_id, z.code, z.name, f.canvas_width, f.canvas_height, f.row_version
            FROM table_mgmt.zones z
            LEFT JOIN table_mgmt.zone_floor_plans f ON f.zone_id = z.zone_id
            WHERE z.zone_id = @zone_id;
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("zone_id", zoneId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(3))
                return null;
            readZoneId = reader.GetGuid(0);
            zoneCode = reader.GetString(1);
            zoneName = reader.GetString(2);
            canvasWidth = reader.GetInt32(3);
            canvasHeight = reader.GetInt32(4);
            rowVersion = reader.GetInt64(5);
        }

        var seatsByTable = await ReadSeatsAsync(connection, transaction, zoneId, cancellationToken);
        var tables = new List<FloorTableLayout>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT t.table_id, t.table_number, t.capacity, t.active, t.current_status,
                   t.current_order_id, t.current_bill_id, t.row_version,
                   l.x, l.y, l.width, l.height, l.shape, l.rotation_degrees, l.row_version,
                   r.table_reservation_id, r.party_size, r.reserved_at, r.expires_at,
                   m.merge_group_id, COALESCE(m.primary_table_id = t.table_id, FALSE)
            FROM table_mgmt.tables t
            LEFT JOIN table_mgmt.table_layouts l ON l.table_id = t.table_id AND l.zone_id = t.zone_id
            LEFT JOIN LATERAL (
                SELECT table_reservation_id, party_size, reserved_at, expires_at
                FROM table_mgmt.table_reservations
                WHERE table_id = t.table_id AND status = 'Active'
                ORDER BY reserved_at DESC, table_reservation_id
                LIMIT 1
            ) r ON TRUE
            LEFT JOIN LATERAL (
                SELECT merge_group_id, primary_table_id
                FROM table_mgmt.table_merges
                WHERE status = 'Active'
                  AND (primary_table_id = t.table_id OR merged_table_id = t.table_id)
                ORDER BY merged_at DESC, table_merge_id
                LIMIT 1
            ) m ON TRUE
            WHERE t.zone_id = @zone_id
            ORDER BY t.table_number, t.table_id;
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("zone_id", zoneId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var tableId = reader.GetGuid(0);
                tables.Add(new FloorTableLayout(
                    tableId,
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetBoolean(3),
                    Enum.Parse<TableState>(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : reader.GetGuid(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    reader.GetInt64(7),
                    reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    reader.IsDBNull(10) ? null : reader.GetInt32(10),
                    reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    reader.IsDBNull(12) ? null : Enum.Parse<FloorTableShape>(reader.GetString(12)),
                    reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                    reader.IsDBNull(15) ? null : reader.GetGuid(15),
                    reader.IsDBNull(16) ? null : reader.GetInt32(16),
                    reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17),
                    reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18),
                    reader.IsDBNull(19) ? null : reader.GetGuid(19),
                    reader.GetBoolean(20),
                    seatsByTable.GetValueOrDefault(tableId, [])));
            }
        }

        return new FloorPlanSnapshot(
            readZoneId,
            zoneCode,
            zoneName,
            canvasWidth,
            canvasHeight,
            rowVersion,
            tables);
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<FloorSeat>>> ReadSeatsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid zoneId,
        CancellationToken cancellationToken)
    {
        var mutable = new Dictionary<Guid, List<FloorSeat>>();
        await using var command = new NpgsqlCommand(
            """
            SELECT s.table_id, s.seat_id, s.seat_number, s.label, s.x, s.y, s.row_version
            FROM table_mgmt.table_seats s
            JOIN table_mgmt.tables t ON t.table_id = s.table_id
            WHERE t.zone_id = @zone_id
            ORDER BY s.table_id, s.seat_number, s.seat_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("zone_id", zoneId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tableId = reader.GetGuid(0);
            if (!mutable.TryGetValue(tableId, out var seats))
            {
                seats = [];
                mutable.Add(tableId, seats);
            }
            seats.Add(new FloorSeat(
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt64(6)));
        }
        return mutable.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<FloorSeat>)pair.Value);
    }
}
