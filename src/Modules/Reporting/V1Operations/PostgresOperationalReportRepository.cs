using System.Data;
using System.Data.Common;
using System.Text.Json;
using ALKAROS.Reporting.BusinessDayTotals;

namespace ALKAROS.Reporting.V1Operations;

/// <summary>
/// Repository interface for operational reporting data access (V1-RPT-001).
/// </summary>
public interface IOperationalReportRepository
{
    Task<BusinessDayRecord> OpenBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken = default);

    Task<BusinessDayRecord> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        decimal totalRevenue,
        int totalOrders,
        int cancelledItems,
        int printFailures,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the business day inside one transaction: the figures are read (<paramref name="readFigures"/>) and judged
    /// under the day's lock, an unacknowledged open item rolls everything back, the day, the waiter and print-station
    /// summaries are written, and <paramref name="inTransaction"/> (the audit event) commits with them.
    /// </summary>
    Task<BusinessDayReportResult> CloseBusinessDayWithFiguresAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        bool acknowledgeOpenItems,
        Func<DbConnection, DbTransaction, CancellationToken, Task<BusinessDayFigures>> readFigures,
        Func<BusinessDayRecord, BusinessDayFigures, DbConnection, DbTransaction, CancellationToken, Task>? inTransaction = null,
        CancellationToken cancellationToken = default);

    Task<BusinessDayRecord?> GetBusinessDayByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<BusinessDayRecord?> GetActiveBusinessDayAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WaiterPerformanceRecord>> GetWaiterSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintErrorSummaryRecord>> GetPrintErrorSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL implementation of <see cref="IOperationalReportRepository"/> (V1-RPT-001).
/// </summary>
public sealed class PostgresOperationalReportRepository : IOperationalReportRepository
{
    private const string BusinessDaysTable = "reporting.daily_business_days";
    private const string WaiterSummariesTable = "reporting.waiter_performance_summaries";
    private const string PrintSummariesTable = "reporting.print_error_summaries";

    private const string DayColumns =
        "business_day_id, business_date, opened_at, closed_at, status, total_revenue, total_orders_count, " +
        "total_cancelled_items_count, total_print_failures_count, total_tips_amount, open_cash_sessions_count, " +
        "open_bills_count, unsettled_payments_count, open_items_acknowledged, table_breakdown::text, cash_reconciliation::text";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // Defensive ceiling for a filtered list read: a real filter returns
    // far fewer rows. Hitting this means the filter is too broad, or the
    // relation outgrew its assumption — fail loud, do not load unboundedly.
    private const int MaxUnpagedRows = 5000;

    private readonly DbDataSource _dataSource;

    public PostgresOperationalReportRepository(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<BusinessDayRecord> OpenBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();

        const string sql = $"""
            INSERT INTO {BusinessDaysTable} (
                business_day_id, business_date, opened_at, closed_at, status, total_revenue, total_orders_count, total_cancelled_items_count, total_print_failures_count
            ) VALUES (
                @id, @date, @opened, NULL, 'Open', 0.00, 0, 0, 0
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended('alkaros.reporting.business_day', 0));";
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = $"SELECT status FROM {BusinessDaysTable} WHERE business_date = @date;";
            AddParameter(checkCommand, "date", businessDate.ToDateTime(TimeOnly.MinValue));
            var status = await checkCommand.ExecuteScalarAsync(cancellationToken);
            if (status is not null)
                throw new BusinessDayAlreadyOpenException(businessDate);
        }

        await using (var activeCommand = connection.CreateCommand())
        {
            activeCommand.Transaction = transaction;
            activeCommand.CommandText = $"SELECT business_date FROM {BusinessDaysTable} WHERE status = 'Open' LIMIT 1;";
            var activeDate = await activeCommand.ExecuteScalarAsync(cancellationToken);
            if (activeDate is DateTime active)
                throw new InvalidBusinessDayOperationException($"An active business day is already open for date '{active:yyyy-MM-dd}'. Close it first.");
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        AddParameter(cmd, "id", id);
        AddParameter(cmd, "date", businessDate.ToDateTime(TimeOnly.MinValue));
        AddParameter(cmd, "opened", openedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BusinessDayRecord(
            id,
            businessDate,
            openedAt,
            null,
            BusinessDayStatus.Open,
            0.00m,
            0,
            0,
            0);
    }

    public async Task<BusinessDayRecord> CloseBusinessDayAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        decimal totalRevenue,
        int totalOrders,
        int cancelledItems,
        int printFailures,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetBusinessDayByDateAsync(businessDate, cancellationToken);
        if (existing is null)
        {
            throw new BusinessDayNotFoundException(businessDate);
        }

        if (existing.Status == BusinessDayStatus.Closed)
        {
            throw new InvalidBusinessDayOperationException($"Business day '{businessDate:yyyy-MM-dd}' is already closed.");
        }

        const string sql = $"""
            UPDATE {BusinessDaysTable}
            SET status = 'Closed', closed_at = @closed, total_revenue = @revenue, total_orders_count = @orders, total_cancelled_items_count = @cancelled, total_print_failures_count = @prints
            WHERE business_date = @date AND status = 'Open';
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "date", businessDate.ToDateTime(TimeOnly.MinValue));
        AddParameter(cmd, "closed", closedAt);
        AddParameter(cmd, "revenue", totalRevenue);
        AddParameter(cmd, "orders", totalOrders);
        AddParameter(cmd, "cancelled", cancelledItems);
        AddParameter(cmd, "prints", printFailures);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidBusinessDayOperationException($"Business day '{businessDate:yyyy-MM-dd}' was closed by another operation.");

        return existing with
        {
            Status = BusinessDayStatus.Closed,
            ClosedAt = closedAt,
            TotalRevenue = totalRevenue,
            TotalOrdersCount = totalOrders,
            TotalCancelledItemsCount = cancelledItems,
            TotalPrintFailuresCount = printFailures
        };
    }

    public async Task<BusinessDayReportResult> CloseBusinessDayWithFiguresAsync(
        DateOnly businessDate,
        DateTimeOffset closedAt,
        bool acknowledgeOpenItems,
        Func<DbConnection, DbTransaction, CancellationToken, Task<BusinessDayFigures>> readFigures,
        Func<BusinessDayRecord, BusinessDayFigures, DbConnection, DbTransaction, CancellationToken, Task>? inTransaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readFigures);

        var dateParam = businessDate.ToDateTime(TimeOnly.MinValue);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Serialize against concurrent open/close on the business day.
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended('alkaros.reporting.business_day', 0));";
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        BusinessDayRecord existing;
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText = $"""
                SELECT {DayColumns}
                FROM {BusinessDaysTable}
                WHERE business_date = @date
                FOR UPDATE;
                """;
            AddParameter(readCommand, "date", dateParam);
            await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new BusinessDayNotFoundException(businessDate);
            }

            existing = ReadBusinessDay(reader);
        }

        if (existing.Status == BusinessDayStatus.Closed)
        {
            throw new InvalidBusinessDayOperationException($"Business day '{businessDate:yyyy-MM-dd}' is already closed.");
        }

        // Read and judged inside the closing transaction: what the manager was warned about is what is stored.
        var figures = await readFigures(connection, transaction, cancellationToken);
        if (figures.OpenItems.Any && !acknowledgeOpenItems)
        {
            throw new BusinessDayOpenItemsNotAcknowledgedException(
                businessDate, figures.OpenItems.CashSessions, figures.OpenItems.Bills, figures.OpenItems.UnsettledPayments);
        }

        var acknowledged = figures.OpenItems.Any;
        await using (var closeCommand = connection.CreateCommand())
        {
            closeCommand.Transaction = transaction;
            closeCommand.CommandText = $"""
                UPDATE {BusinessDaysTable}
                SET status = 'Closed', closed_at = @closed, total_revenue = @revenue, total_orders_count = @orders,
                    total_cancelled_items_count = @cancelled, total_print_failures_count = @prints, total_tips_amount = @tips,
                    open_cash_sessions_count = @open_sessions, open_bills_count = @open_bills,
                    unsettled_payments_count = @unsettled, open_items_acknowledged = @acknowledged,
                    table_breakdown = @tables::jsonb, cash_reconciliation = @cash::jsonb
                WHERE business_date = @date AND status = 'Open';
                """;
            AddParameter(closeCommand, "date", dateParam);
            AddParameter(closeCommand, "closed", closedAt);
            AddParameter(closeCommand, "revenue", figures.Revenue);
            AddParameter(closeCommand, "orders", figures.OrderCount);
            AddParameter(closeCommand, "cancelled", figures.CancelledItems);
            AddParameter(closeCommand, "prints", figures.PrintFailures);
            AddParameter(closeCommand, "tips", figures.Tips);
            AddParameter(closeCommand, "open_sessions", figures.OpenItems.CashSessions);
            AddParameter(closeCommand, "open_bills", figures.OpenItems.Bills);
            AddParameter(closeCommand, "unsettled", figures.OpenItems.UnsettledPayments);
            AddParameter(closeCommand, "acknowledged", acknowledged);
            AddParameter(closeCommand, "tables", JsonSerializer.Serialize(figures.Tables, Json));
            AddParameter(closeCommand, "cash", JsonSerializer.Serialize(figures.Cash, Json));

            var affected = await closeCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new InvalidBusinessDayOperationException($"Business day '{businessDate:yyyy-MM-dd}' was closed by another operation.");
            }
        }

        foreach (var waiter in figures.Waiters)
        {
            await using var waiterCommand = connection.CreateCommand();
            waiterCommand.Transaction = transaction;
            waiterCommand.CommandText = $"""
                INSERT INTO {WaiterSummariesTable} (
                    summary_id, business_date, waiter_user_id, orders_served_count, total_sales_amount, cancellations_count, discounts_applied_amount, captured_at
                ) VALUES (
                    @id, @date, @waiter, @orders, @sales, @cancellations, @discounts, @captured
                );
                """;
            AddParameter(waiterCommand, "id", Guid.NewGuid());
            AddParameter(waiterCommand, "date", dateParam);
            AddParameter(waiterCommand, "waiter", waiter.WaiterUserId);
            AddParameter(waiterCommand, "orders", waiter.Orders);
            AddParameter(waiterCommand, "sales", waiter.Sales);
            AddParameter(waiterCommand, "cancellations", waiter.Cancellations);
            AddParameter(waiterCommand, "discounts", waiter.Discounts);
            AddParameter(waiterCommand, "captured", closedAt);
            await waiterCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var print in figures.PrintStations)
        {
            await using var printCommand = connection.CreateCommand();
            printCommand.Transaction = transaction;
            printCommand.CommandText = $"""
                INSERT INTO {PrintSummariesTable} (
                    error_summary_id, business_date, station_name, total_print_jobs, failed_print_jobs, recovered_print_jobs, captured_at
                ) VALUES (
                    @id, @date, @station, @total, @failed, @recovered, @captured
                );
                """;
            AddParameter(printCommand, "id", Guid.NewGuid());
            AddParameter(printCommand, "date", dateParam);
            AddParameter(printCommand, "station", print.Station);
            AddParameter(printCommand, "total", print.Total);
            AddParameter(printCommand, "failed", print.Failed);
            AddParameter(printCommand, "recovered", print.Recovered);
            AddParameter(printCommand, "captured", closedAt);
            await printCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var persistedWaiters = await ReadWaiterSummariesAsync(connection, transaction, dateParam, cancellationToken);
        var persistedPrints = await ReadPrintErrorSummariesAsync(connection, transaction, dateParam, cancellationToken);

        var closedDay = existing with
        {
            Status = BusinessDayStatus.Closed,
            ClosedAt = closedAt,
            TotalRevenue = figures.Revenue,
            TotalOrdersCount = figures.OrderCount,
            TotalCancelledItemsCount = figures.CancelledItems,
            TotalPrintFailuresCount = figures.PrintFailures,
            TotalTipsAmount = figures.Tips,
            OpenItemsAtClose = figures.OpenItems,
            OpenItemsAcknowledged = acknowledged,
            TableBreakdown = figures.Tables,
            CashReconciliation = figures.Cash,
        };

        if (inTransaction is not null)
            await inTransaction(closedDay, figures, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new BusinessDayReportResult(closedDay, persistedWaiters, persistedPrints);
    }

    private static async Task<IReadOnlyList<WaiterPerformanceRecord>> ReadWaiterSummariesAsync(
        DbConnection connection,
        DbTransaction transaction,
        DateTime businessDate,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            SELECT summary_id, business_date, waiter_user_id, orders_served_count, total_sales_amount, cancellations_count, discounts_applied_amount, captured_at
            FROM {WaiterSummariesTable}
            WHERE business_date = @date
            ORDER BY total_sales_amount DESC;
            """;
        AddParameter(cmd, "date", businessDate);

        var list = new List<WaiterPerformanceRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new WaiterPerformanceRecord(
                reader.GetGuid(0),
                DateOnly.FromDateTime(reader.GetDateTime(1)),
                reader.GetGuid(2),
                reader.GetInt32(3),
                reader.GetDecimal(4),
                reader.GetInt32(5),
                reader.GetDecimal(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<PrintErrorSummaryRecord>> ReadPrintErrorSummariesAsync(
        DbConnection connection,
        DbTransaction transaction,
        DateTime businessDate,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            SELECT error_summary_id, business_date, station_name, total_print_jobs, failed_print_jobs, recovered_print_jobs, captured_at
            FROM {PrintSummariesTable}
            WHERE business_date = @date
            ORDER BY station_name ASC;
            """;
        AddParameter(cmd, "date", businessDate);

        var list = new List<PrintErrorSummaryRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new PrintErrorSummaryRecord(
                reader.GetGuid(0),
                DateOnly.FromDateTime(reader.GetDateTime(1)),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return list;
    }

    public async Task<BusinessDayRecord?> GetBusinessDayByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {DayColumns}
            FROM {BusinessDaysTable}
            WHERE business_date = @date;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "date", businessDate.ToDateTime(TimeOnly.MinValue));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadBusinessDay(reader);
    }

    public async Task<BusinessDayRecord?> GetActiveBusinessDayAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {DayColumns}
            FROM {BusinessDaysTable}
            WHERE status = 'Open'
            ORDER BY opened_at DESC
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadBusinessDay(reader);
    }

    public async Task<IReadOnlyList<WaiterPerformanceRecord>> GetWaiterSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT summary_id, business_date, waiter_user_id, orders_served_count, total_sales_amount, cancellations_count, discounts_applied_amount, captured_at
            FROM {WaiterSummariesTable}
            WHERE business_date = @date
            ORDER BY total_sales_amount DESC
            LIMIT {MaxUnpagedRows + 1};
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "date", businessDate.ToDateTime(TimeOnly.MinValue));

        var list = new List<WaiterPerformanceRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new WaiterPerformanceRecord(
                reader.GetGuid(0),
                DateOnly.FromDateTime(reader.GetDateTime(1)),
                reader.GetGuid(2),
                reader.GetInt32(3),
                reader.GetDecimal(4),
                reader.GetInt32(5),
                reader.GetDecimal(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }

        if (list.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetWaiterSummariesByDateAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return list;
    }

    public async Task<IReadOnlyList<PrintErrorSummaryRecord>> GetPrintErrorSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT error_summary_id, business_date, station_name, total_print_jobs, failed_print_jobs, recovered_print_jobs, captured_at
            FROM {PrintSummariesTable}
            WHERE business_date = @date
            ORDER BY station_name ASC
            LIMIT {MaxUnpagedRows + 1};
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "date", businessDate.ToDateTime(TimeOnly.MinValue));

        var list = new List<PrintErrorSummaryRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new PrintErrorSummaryRecord(
                reader.GetGuid(0),
                DateOnly.FromDateTime(reader.GetDateTime(1)),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }

        if (list.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetPrintErrorSummariesByDateAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return list;
    }

    private static BusinessDayRecord ReadBusinessDay(DbDataReader reader)
    {
        return new BusinessDayRecord(
            reader.GetGuid(0),
            DateOnly.FromDateTime(reader.GetDateTime(1)),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            Enum.Parse<BusinessDayStatus>(reader.GetString(4), ignoreCase: true),
            reader.GetDecimal(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetDecimal(9),
            new OpenItems(reader.GetInt32(10), reader.GetInt32(11), reader.GetInt32(12)),
            reader.GetBoolean(13),
            JsonSerializer.Deserialize<List<TableFigure>>(reader.GetString(14), Json),
            reader.IsDBNull(15) ? null : JsonSerializer.Deserialize<CashReconciliationFigure>(reader.GetString(15), Json));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
