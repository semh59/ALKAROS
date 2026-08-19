using System.Data;
using System.Data.Common;

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

    Task<BusinessDayRecord?> GetBusinessDayByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<BusinessDayRecord?> GetActiveBusinessDayAsync(
        CancellationToken cancellationToken = default);

    Task RecordWaiterSummaryAsync(
        WaiterPerformanceRecord summary,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WaiterPerformanceRecord>> GetWaiterSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task RecordPrintErrorSummaryAsync(
        PrintErrorSummaryRecord summary,
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

    public async Task<BusinessDayRecord?> GetBusinessDayByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT business_day_id, business_date, opened_at, closed_at, status, total_revenue, total_orders_count, total_cancelled_items_count, total_print_failures_count
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
            SELECT business_day_id, business_date, opened_at, closed_at, status, total_revenue, total_orders_count, total_cancelled_items_count, total_print_failures_count
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

    public async Task RecordWaiterSummaryAsync(
        WaiterPerformanceRecord summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        const string sql = $"""
            INSERT INTO {WaiterSummariesTable} (
                summary_id, business_date, waiter_user_id, orders_served_count, total_sales_amount, cancellations_count, discounts_applied_amount, captured_at
            ) VALUES (
                @id, @date, @waiter, @orders, @sales, @cancellations, @discounts, @captured
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", summary.SummaryId == Guid.Empty ? Guid.NewGuid() : summary.SummaryId);
        AddParameter(cmd, "date", summary.BusinessDate.ToDateTime(TimeOnly.MinValue));
        AddParameter(cmd, "waiter", summary.WaiterUserId);
        AddParameter(cmd, "orders", summary.OrdersServedCount);
        AddParameter(cmd, "sales", summary.TotalSalesAmount);
        AddParameter(cmd, "cancellations", summary.CancellationsCount);
        AddParameter(cmd, "discounts", summary.DiscountsAppliedAmount);
        AddParameter(cmd, "captured", summary.CapturedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WaiterPerformanceRecord>> GetWaiterSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT summary_id, business_date, waiter_user_id, orders_served_count, total_sales_amount, cancellations_count, discounts_applied_amount, captured_at
            FROM {WaiterSummariesTable}
            WHERE business_date = @date
            ORDER BY total_sales_amount DESC;
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

        return list;
    }

    public async Task RecordPrintErrorSummaryAsync(
        PrintErrorSummaryRecord summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        const string sql = $"""
            INSERT INTO {PrintSummariesTable} (
                error_summary_id, business_date, station_name, total_print_jobs, failed_print_jobs, recovered_print_jobs, captured_at
            ) VALUES (
                @id, @date, @station, @total, @failed, @recovered, @captured
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", summary.ErrorSummaryId == Guid.Empty ? Guid.NewGuid() : summary.ErrorSummaryId);
        AddParameter(cmd, "date", summary.BusinessDate.ToDateTime(TimeOnly.MinValue));
        AddParameter(cmd, "station", summary.StationName);
        AddParameter(cmd, "total", summary.TotalPrintJobs);
        AddParameter(cmd, "failed", summary.FailedPrintJobs);
        AddParameter(cmd, "recovered", summary.RecoveredPrintJobs);
        AddParameter(cmd, "captured", summary.CapturedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrintErrorSummaryRecord>> GetPrintErrorSummariesByDateAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT error_summary_id, business_date, station_name, total_print_jobs, failed_print_jobs, recovered_print_jobs, captured_at
            FROM {PrintSummariesTable}
            WHERE business_date = @date
            ORDER BY station_name ASC;
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
            reader.GetInt32(8));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
