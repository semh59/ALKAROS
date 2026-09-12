using ALKAROS.Tables.Reservations;
using ALKAROS.Tables.TableLifecycle;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Tables;

public sealed class TableManagementStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ITableRepository _repository;
    private readonly ITableReservationRepository _reservationRepository;

    public TableManagementStore(
        NpgsqlDataSource dataSource,
        ITableRepository repository,
        ITableReservationRepository reservationRepository)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _reservationRepository = reservationRepository ?? throw new ArgumentNullException(nameof(reservationRepository));
    }

    // V1-RMD-135: found by an independent audit (2026-09-09) — WaiterPwa's
    // table list always rendered every table's running total as empty
    // because TableDto never carried one at all; the client was
    // reading fields ("amount"/"currentAmount") that simply don't exist on
    // the real contract. CurrentOrderTotal closes that gap: the same
    // formula Order.Total itself uses (SUM of active — Draft or Active —
    // items' persisted gross_amount), read directly here rather than
    // loading a full Order aggregate per table row. A table with no
    // current order (or an order with no items yet) reports 0m, matching
    // how the client already treats "nothing to show".
    private const string CurrentOrderTotalSql =
        """
        COALESCE((
            SELECT SUM(oi.gross_amount)
            FROM orders.order_items oi
            WHERE oi.order_id = t.current_order_id AND oi.status IN ('Draft', 'Active')
        ), 0)
        """;

    // V1-WTR-019: garson-karsilastirma idea #7, the table-ageing indicator —
    // how long the table's current tab has been open, for WaiterPwa's own
    // ageing badge. Same "read model, not a new column" shape
    // CurrentOrderTotalSql above already established: the truth is
    // orders.orders.created_at for the order table_mgmt's own
    // current_order_id points at, not a new timestamp duplicated onto
    // table_mgmt.tables itself (which would need its own write-path
    // discipline to ever stay in sync). Null when there is no current
    // order.
    private const string CurrentOrderOpenedAtSql =
        """
        (SELECT o.created_at FROM orders.orders o WHERE o.order_id = t.current_order_id)
        """;

    public async Task<(Table Table, decimal CurrentOrderTotal, DateTimeOffset? CurrentOrderOpenedAt, DateTimeOffset StatusChangedAt)?> GetAsync(
        Guid tableId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT t.table_id, t.table_number, t.zone_id, t.capacity, t.active, t.current_status,
                   t.current_order_id, t.current_bill_id, t.row_version, {CurrentOrderTotalSql}, {CurrentOrderOpenedAtSql},
                   t.status_changed_at
            FROM table_mgmt.tables t
            WHERE t.table_id = @table_id;
            """);
        command.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return (Read(reader), reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            reader.GetFieldValue<DateTimeOffset>(11));
    }

    public async Task<IReadOnlyList<(Table Table, decimal CurrentOrderTotal, DateTimeOffset? CurrentOrderOpenedAt, DateTimeOffset StatusChangedAt)>> GetAllAsync(
        Guid? zoneId,
        CancellationToken cancellationToken = default)
    {
        var tables = new List<(Table, decimal, DateTimeOffset?, DateTimeOffset)>();
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT t.table_id, t.table_number, t.zone_id, t.capacity, t.active, t.current_status,
                   t.current_order_id, t.current_bill_id, t.row_version, {CurrentOrderTotalSql}, {CurrentOrderOpenedAtSql},
                   t.status_changed_at
            FROM table_mgmt.tables t
            WHERE @zone_id IS NULL OR t.zone_id = @zone_id
            ORDER BY t.table_number, t.table_id;
            """);
        command.Parameters.Add("zone_id", NpgsqlDbType.Uuid).Value = zoneId ?? (object)DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables.Add((Read(reader), reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
                reader.GetFieldValue<DateTimeOffset>(11)));
        return tables;
    }

    public async Task<Table> CreateAsync(
        CreateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TableContractMapper.RequireCreateVersion(request.ExpectedRowVersion);
        var table = new Table(
            Guid.NewGuid(),
            request.TableNumber,
            request.ZoneId,
            request.Capacity,
            request.Active);
        await _repository.AddAsync(table, cancellationToken);
        return await _repository.GetByIdAsync(table.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created table could not be read back.");
    }

    public async Task<Table> UpdateAsync(
        Guid tableId,
        UpdateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureId(tableId);
        ArgumentNullException.ThrowIfNull(request);
        var expected = TableContractMapper.RequiredVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        _ = new Table(tableId, request.TableNumber, request.ZoneId, request.Capacity, request.Active);

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE table_mgmt.tables
            SET table_number = @table_number,
                zone_id = @zone_id,
                capacity = @capacity,
                active = @active,
                row_version = row_version + 1
            WHERE table_id = @table_id AND row_version = @expected_row_version
            RETURNING table_id, table_number, zone_id, capacity, active, current_status,
                      current_order_id, current_bill_id, row_version;
            """);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("table_number", request.TableNumber);
        command.Parameters.AddWithValue("zone_id", request.ZoneId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("capacity", request.Capacity);
        command.Parameters.AddWithValue("active", request.Active);
        command.Parameters.AddWithValue("expected_row_version", expected);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return Read(reader);

        return await MissingOrConflictAsync(tableId, expected, cancellationToken);
    }

    public async Task<Table> ChangeStatusAsync(
        Guid tableId,
        ChangeTableStatusRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        EnsureId(tableId);
        ArgumentNullException.ThrowIfNull(request);
        var expected = TableContractMapper.RequiredVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        if (!Enum.TryParse<TableState>(request.Status, ignoreCase: true, out var target)
            || !Enum.IsDefined(target))
        {
            throw new ArgumentException("Status is not a supported table state.", nameof(request));
        }

        // Found by an independent audit (2026-09-09): this generic endpoint
        // only requires ApplicationPermissions.TablesStatus, so setting
        // target=Reserved here bypassed the dedicated
        // ApplicationPermissions.TablesReserve permission entirely — anyone
        // who could change a table's status at all could reserve it. Worse,
        // a bare table-row update never created a matching
        // table_mgmt.table_reservations row, breaking the invariant every
        // other reservation code path relies on (Reserved iff an Active
        // reservation exists — see the Reserved-release comment below).
        // Reserving a table is only ever valid through POST .../reservations,
        // which enforces the right permission and creates the reservation
        // record atomically with the table's status change.
        if (target == TableState.Reserved)
        {
            throw new TableManagementConflictException(
                "Reserved status cannot be set through the generic status endpoint; use POST .../reservations instead.");
        }

        var current = await _repository.GetByIdAsync(tableId, cancellationToken)
            ?? throw new TableManagementNotFoundException($"Table {tableId} was not found.");
        if (current.RowVersion != expected)
            throw new TableManagementConcurrencyException("table", tableId, expected, current.RowVersion);

        try
        {
            _ = current.TransitionTo(target);
        }
        catch (InvalidOperationException exception)
        {
            throw new TableManagementConflictException(exception.Message, exception);
        }

        // Found by an independent audit (2026-09-07): this generic status
        // change was the ONLY UI-reachable way to release a Reserved table
        // (Claim/Cancel/Expire all require a reservation row version no
        // client could ever obtain before V1-RMD-117) but it never touched
        // table_mgmt.table_reservations — leaving the Active row in place
        // permanently blocked CreateReservationAsync's own "table already
        // has an active reservation" guard from ever reserving that table
        // again. Route Reserved -> Available/Occupied through the same
        // atomic release the dedicated endpoints use instead of a bare
        // table-row update, so the invariant (Reserved iff an Active
        // reservation exists) never desyncs regardless of which path a
        // client used to leave it.
        if (current.State == TableState.Reserved)
        {
            var reservation = await _reservationRepository.GetActiveByTableIdAsync(tableId, cancellationToken);
            if (reservation is not null)
            {
                if (target == TableState.Occupied)
                {
                    await _reservationRepository.ClaimReservationAsync(new ClaimReservationRequest(
                        reservation.Id, reservation.RowVersion, expected, OrderId: null, actorId), cancellationToken);
                }
                else
                {
                    await _reservationRepository.CancelReservationAsync(new CancelReservationRequest(
                        reservation.Id, reservation.RowVersion, expected, actorId,
                        "Released via table status change"), cancellationToken);
                }

                return await _repository.GetByIdAsync(tableId, cancellationToken)
                    ?? throw new TableManagementNotFoundException($"Table {tableId} was not found after status update.");
            }
        }

        try
        {
            await _repository.UpdateStatusAsync(tableId, target, expected, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await MissingOrConflictAsync(tableId, expected, cancellationToken);
        }

        return await _repository.GetByIdAsync(tableId, cancellationToken)
            ?? throw new TableManagementNotFoundException($"Table {tableId} was not found after status update.");
    }

    private async Task<Table> MissingOrConflictAsync(
        Guid tableId,
        long expected,
        CancellationToken cancellationToken)
    {
        var actual = await _repository.GetByIdAsync(tableId, cancellationToken);
        if (actual is null)
            throw new TableManagementNotFoundException($"Table {tableId} was not found.");
        throw new TableManagementConcurrencyException("table", tableId, expected, actual.RowVersion);
    }

    private static Table Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetGuid(2),
        reader.GetInt32(3),
        reader.GetBoolean(4),
        Enum.Parse<TableState>(reader.GetString(5)),
        reader.IsDBNull(6) ? null : reader.GetGuid(6),
        reader.IsDBNull(7) ? null : reader.GetGuid(7),
        reader.GetInt64(8));

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Table ID cannot be empty.", nameof(id));
    }
}
