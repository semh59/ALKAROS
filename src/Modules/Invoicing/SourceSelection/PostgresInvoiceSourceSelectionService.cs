using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Invoicing.SourceSelection;

/// <summary>
/// V14-INV-001 over PostgreSQL. Every write runs in one transaction under an advisory lock: period closing under one
/// lock for all periods (the overlap check and the insert are one decision), selection under a lock per period and
/// customer (two concurrent selections of the same customer resolve to one set). The partial unique index
/// ux_invoice_source_lines_live_transaction is the final guard that a transaction is in at most one live set.
/// </summary>
public sealed class PostgresInvoiceSourceSelectionService : IInvoiceSourceSelectionService
{
    /// <summary>The most transactions one customer's source set may hold; a larger period is split instead.</summary>
    public const int MaxLinesPerSet = 5000;

    private const int MaxCustomersPerRun = 500;

    private const string PeriodColumns = "period_id, period_start, period_end, closed_at, closed_by";

    // A transaction is uninvoiced when no live source line holds it. Invoice rows are the invoice itself and never
    // re-enter a period (V0-DOM-007 invariant 1).
    private const string EligibleTransactionsSql = """
        SELECT t.id, t.transaction_type, t.direction, t.amount, t.occurred_at
        FROM customer_account.account_transactions AS t
        WHERE t.customer_id = @customer_id
          AND t.transaction_type <> 'Invoice'
          AND t.occurred_at >= (@period_start::timestamp AT TIME ZONE 'Europe/Istanbul')
          AND t.occurred_at < (@period_end::timestamp AT TIME ZONE 'Europe/Istanbul')
          AND NOT EXISTS (
              SELECT 1 FROM invoicing.invoice_source_lines AS l
              WHERE l.transaction_id = t.id AND NOT l.cancelled)
        ORDER BY t.occurred_at, t.id
        LIMIT @limit;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresInvoiceSourceSelectionService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<InvoicePeriod> ClosePeriodAsync(
        DateOnly periodStart, DateOnly periodEnd, Guid closedBy, CancellationToken cancellationToken = default)
    {
        if (periodStart >= periodEnd)
            throw new InvoicePeriodRangeInvalidException(periodStart, periodEnd);
        if (closedBy == Guid.Empty)
            throw new ArgumentException("The closing operator is required.", nameof(closedBy));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, "invoice-periods", cancellationToken);

        await using (var existing = Command(connection, transaction, $"""
            SELECT {PeriodColumns} FROM invoicing.invoice_periods
            WHERE period_start = @period_start AND period_end = @period_end;
            """))
        {
            AddDate(existing, "period_start", periodStart);
            AddDate(existing, "period_end", periodEnd);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var period = ReadPeriod(reader);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return period;
            }
        }

        await using (var overlap = Command(connection, transaction, """
            SELECT EXISTS (
                SELECT 1 FROM invoicing.invoice_periods
                WHERE period_start < @period_end AND period_end > @period_start);
            """))
        {
            AddDate(overlap, "period_start", periodStart);
            AddDate(overlap, "period_end", periodEnd);
            if ((bool)(await overlap.ExecuteScalarAsync(cancellationToken))!)
                throw new InvoicePeriodOverlapException(periodStart, periodEnd);
        }

        await using (var today = Command(connection, transaction,
            "SELECT (now() AT TIME ZONE 'Europe/Istanbul')::date;"))
        {
            var istanbulToday = DateOnly.FromDateTime((DateTime)(await today.ExecuteScalarAsync(cancellationToken))!);
            // End is exclusive: a period ending today covers up to yesterday and has ended.
            if (periodEnd > istanbulToday)
                throw new InvoicePeriodNotEndedException(periodEnd);
        }

        InvoicePeriod closed;
        await using (var insert = Command(connection, transaction, $"""
            INSERT INTO invoicing.invoice_periods (period_id, period_start, period_end, closed_by)
            VALUES (@period_id, @period_start, @period_end, @closed_by)
            RETURNING {PeriodColumns};
            """))
        {
            insert.Parameters.AddWithValue("period_id", Guid.NewGuid());
            AddDate(insert, "period_start", periodStart);
            AddDate(insert, "period_end", periodEnd);
            insert.Parameters.AddWithValue("closed_by", closedBy);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            closed = ReadPeriod(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return closed;
    }

    public async Task<InvoicePeriod?> GetPeriodAsync(Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadPeriodAsync(connection, null, periodId, cancellationToken);
    }

    public async Task<InvoiceSourceSelectionResult> SelectAsync(
        Guid periodId, Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await SelectInTransactionAsync(connection, transaction, periodId, customerId, cancellationToken)
            ?? throw new NoInvoiceableTransactionsException(periodId, customerId);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<InvoiceSourceSelectionResult>> SelectAllAsync(
        Guid periodId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var period = await ReadPeriodAsync(connection, null, periodId, cancellationToken)
            ?? throw new InvoicePeriodNotFoundException(periodId);

        var customers = new List<Guid>();
        await using (var command = Command(connection, null, """
            SELECT DISTINCT t.customer_id
            FROM customer_account.account_transactions AS t
            WHERE t.transaction_type <> 'Invoice'
              AND t.occurred_at >= (@period_start::timestamp AT TIME ZONE 'Europe/Istanbul')
              AND t.occurred_at < (@period_end::timestamp AT TIME ZONE 'Europe/Istanbul')
              AND NOT EXISTS (
                  SELECT 1 FROM invoicing.invoice_source_lines AS l
                  WHERE l.transaction_id = t.id AND NOT l.cancelled)
            ORDER BY t.customer_id
            LIMIT @limit;
            """))
        {
            AddDate(command, "period_start", period.PeriodStart);
            AddDate(command, "period_end", period.PeriodEnd);
            command.Parameters.AddWithValue("limit", MaxCustomersPerRun);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                customers.Add(reader.GetGuid(0));
        }

        var results = new List<InvoiceSourceSelectionResult>(customers.Count);
        foreach (var customerId in customers)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var result = await SelectInTransactionAsync(connection, transaction, periodId, customerId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (result is not null)
                results.Add(result);
        }

        return results;
    }

    public async Task<InvoiceSourceSet?> GetSourceSetAsync(Guid sourceSetId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadSourceSetAsync(connection, null, sourceSetId, cancellationToken);
    }

    public async Task CancelAsync(
        Guid sourceSetId, Guid cancelledBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (cancelledBy == Guid.Empty)
            throw new ArgumentException("The cancelling operator is required.", nameof(cancelledBy));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        string status;
        await using (var select = Command(connection, transaction, """
            SELECT status FROM invoicing.invoice_source_sets WHERE source_set_id = @source_set_id FOR UPDATE;
            """))
        {
            select.Parameters.AddWithValue("source_set_id", sourceSetId);
            status = await select.ExecuteScalarAsync(cancellationToken) as string
                ?? throw new InvoiceSourceSetNotFoundException(sourceSetId);
        }

        if (status == nameof(InvoiceSourceSetStatus.Cancelled))
            throw new InvoiceSourceSetAlreadyCancelledException(sourceSetId);

        await using (var cancel = Command(connection, transaction, """
            UPDATE invoicing.invoice_source_sets
            SET status = 'Cancelled', cancelled_at = now(), cancelled_by = @cancelled_by, cancel_reason = @reason
            WHERE source_set_id = @source_set_id;
            UPDATE invoicing.invoice_source_lines SET cancelled = true WHERE source_set_id = @source_set_id;
            """))
        {
            cancel.Parameters.AddWithValue("source_set_id", sourceSetId);
            cancel.Parameters.AddWithValue("cancelled_by", cancelledBy);
            cancel.Parameters.AddWithValue("reason", reason.Trim());
            await cancel.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The selection decision inside the caller's transaction; null when the customer has nothing to invoice and no
    /// live set exists.
    /// </summary>
    private static async Task<InvoiceSourceSelectionResult?> SelectInTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid periodId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        await LockAsync(connection, transaction, $"invoice-source:{periodId:N}:{customerId:N}", cancellationToken);
        var period = await ReadPeriodAsync(connection, transaction, periodId, cancellationToken)
            ?? throw new InvoicePeriodNotFoundException(periodId);

        await using (var live = Command(connection, transaction, """
            SELECT source_set_id FROM invoicing.invoice_source_sets
            WHERE period_id = @period_id AND customer_id = @customer_id AND status = 'Selected';
            """))
        {
            live.Parameters.AddWithValue("period_id", periodId);
            live.Parameters.AddWithValue("customer_id", customerId);
            if (await live.ExecuteScalarAsync(cancellationToken) is Guid existingId)
            {
                var existing = await ReadSourceSetAsync(connection, transaction, existingId, cancellationToken);
                return new InvoiceSourceSelectionResult(existing!, WasAlreadySelected: true);
            }
        }

        var lines = new List<InvoiceSourceLine>();
        await using (var eligible = Command(connection, transaction, EligibleTransactionsSql))
        {
            eligible.Parameters.AddWithValue("customer_id", customerId);
            AddDate(eligible, "period_start", period.PeriodStart);
            AddDate(eligible, "period_end", period.PeriodEnd);
            eligible.Parameters.AddWithValue("limit", MaxLinesPerSet + 1);
            await using var reader = await eligible.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new InvoiceSourceLine(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.GetFieldValue<DateTimeOffset>(4)));
            }
        }

        if (lines.Count == 0)
            return null;
        if (lines.Count > MaxLinesPerSet)
            throw new InvoiceSourceSetTooLargeException(periodId, customerId, MaxLinesPerSet);

        // Magnitudes by direction; an Adjustment carries its own sign in amount (V0-DOM-007 invariant 5).
        var debitTotal = lines.Where(line => line.Direction == "Debit").Sum(line => Math.Abs(line.Amount));
        var creditTotal = lines.Where(line => line.Direction == "Credit").Sum(line => Math.Abs(line.Amount));
        var sourceSetId = Guid.NewGuid();

        await using (var insertSet = Command(connection, transaction, """
            INSERT INTO invoicing.invoice_source_sets
                (source_set_id, period_id, customer_id, debit_total, credit_total, line_count)
            VALUES (@source_set_id, @period_id, @customer_id, @debit_total, @credit_total, @line_count);
            """))
        {
            insertSet.Parameters.AddWithValue("source_set_id", sourceSetId);
            insertSet.Parameters.AddWithValue("period_id", periodId);
            insertSet.Parameters.AddWithValue("customer_id", customerId);
            insertSet.Parameters.AddWithValue("debit_total", debitTotal);
            insertSet.Parameters.AddWithValue("credit_total", creditTotal);
            insertSet.Parameters.AddWithValue("line_count", lines.Count);
            await insertSet.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertLines = Command(connection, transaction, """
            INSERT INTO invoicing.invoice_source_lines
                (source_set_id, transaction_id, transaction_type, direction, amount, occurred_at)
            SELECT @source_set_id, line.transaction_id, line.transaction_type, line.direction, line.amount, line.occurred_at
            FROM unnest(@transaction_ids, @transaction_types, @directions, @amounts, @occurred_ats)
                AS line (transaction_id, transaction_type, direction, amount, occurred_at);
            """))
        {
            insertLines.Parameters.AddWithValue("source_set_id", sourceSetId);
            insertLines.Parameters.AddWithValue("transaction_ids", lines.Select(line => line.TransactionId).ToArray());
            insertLines.Parameters.AddWithValue("transaction_types", lines.Select(line => line.TransactionType).ToArray());
            insertLines.Parameters.AddWithValue("directions", lines.Select(line => line.Direction).ToArray());
            insertLines.Parameters.AddWithValue("amounts", lines.Select(line => line.Amount).ToArray());
            insertLines.Parameters.AddWithValue("occurred_ats", lines.Select(line => line.OccurredAt).ToArray());
            await insertLines.ExecuteNonQueryAsync(cancellationToken);
        }

        var created = await ReadSourceSetAsync(connection, transaction, sourceSetId, cancellationToken);
        return new InvoiceSourceSelectionResult(created!, WasAlreadySelected: false);
    }

    private static async Task<InvoicePeriod?> ReadPeriodAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid periodId, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction,
            $"SELECT {PeriodColumns} FROM invoicing.invoice_periods WHERE period_id = @period_id;");
        command.Parameters.AddWithValue("period_id", periodId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPeriod(reader) : null;
    }

    private static async Task<InvoiceSourceSet?> ReadSourceSetAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid sourceSetId, CancellationToken cancellationToken)
    {
        Guid periodId;
        Guid customerId;
        InvoiceSourceSetStatus status;
        decimal debitTotal;
        decimal creditTotal;
        DateTimeOffset selectedAt;
        await using (var header = Command(connection, transaction, """
            SELECT period_id, customer_id, status, debit_total, credit_total, selected_at
            FROM invoicing.invoice_source_sets WHERE source_set_id = @source_set_id;
            """))
        {
            header.Parameters.AddWithValue("source_set_id", sourceSetId);
            await using var reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            periodId = reader.GetGuid(0);
            customerId = reader.GetGuid(1);
            status = Enum.Parse<InvoiceSourceSetStatus>(reader.GetString(2));
            debitTotal = reader.GetDecimal(3);
            creditTotal = reader.GetDecimal(4);
            selectedAt = reader.GetFieldValue<DateTimeOffset>(5);
        }

        var lines = new List<InvoiceSourceLine>();
        await using (var lineCommand = Command(connection, transaction, """
            SELECT transaction_id, transaction_type, direction, amount, occurred_at
            FROM invoicing.invoice_source_lines
            WHERE source_set_id = @source_set_id
            ORDER BY occurred_at, transaction_id
            LIMIT @limit;
            """))
        {
            lineCommand.Parameters.AddWithValue("source_set_id", sourceSetId);
            lineCommand.Parameters.AddWithValue("limit", MaxLinesPerSet);
            await using var reader = await lineCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new InvoiceSourceLine(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.GetFieldValue<DateTimeOffset>(4)));
            }
        }

        return new InvoiceSourceSet(sourceSetId, periodId, customerId, status, debitTotal, creditTotal, selectedAt, lines);
    }

    private static InvoicePeriod ReadPeriod(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetFieldValue<DateOnly>(1),
            reader.GetFieldValue<DateOnly>(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetGuid(4));

    private static async Task LockAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, "SELECT pg_advisory_xact_lock(hashtext(@key)::bigint);");
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddDate(NpgsqlCommand command, string name, DateOnly value)
        => command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Date) { Value = value });

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
