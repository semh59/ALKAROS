using ALKAROS.Invoicing.Generation;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Invoicing.SourceTraceability;

public sealed class PostgresInvoiceTraceabilityService(NpgsqlDataSource dataSource) : IInvoiceTraceabilityService
{
    private sealed record Line(int LineNumber, decimal TaxRate, decimal GrossAmount);

    private sealed record Charge(Guid TransactionId, decimal Amount, Guid? BillId);

    private sealed record Link(int LineNumber, Guid TransactionId, decimal Amount);

    public async Task<InvoiceTraceResult> RecordAsync(
        Guid invoiceId, Guid recordedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceSetId = await LockInvoiceAsync(connection, transaction, invoiceId, cancellationToken)
            ?? throw new InvoiceTraceInvoiceNotFoundException(invoiceId);
        var existing = await ReadAsync(connection, transaction, invoiceId, sourceSetId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new InvoiceTraceResult(existing, WasAlreadyRecorded: true);
        }

        var lines = await ReadLinesAsync(connection, transaction, invoiceId, cancellationToken);
        var links = await AllocateAsync(connection, transaction, invoiceId, sourceSetId, lines, cancellationToken);
        await InsertAsync(connection, transaction, invoiceId, sourceSetId, links, recordedBy, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new InvoiceTraceResult(
            await GetAsync(invoiceId, cancellationToken) ?? throw new InvalidOperationException(
                $"Trace of invoice {invoiceId} vanished right after it was recorded."),
            WasAlreadyRecorded: false);
    }

    public async Task<InvoiceTrace?> GetAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var find = new NpgsqlCommand(
            "SELECT source_set_id FROM invoicing.invoices WHERE invoice_id = @invoice_id;", connection);
        find.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
        return await find.ExecuteScalarAsync(cancellationToken) is Guid sourceSetId
            ? await ReadAsync(connection, null, invoiceId, sourceSetId, cancellationToken)
            : null;
    }

    private static async Task<Guid?> LockInvoiceAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid invoiceId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT source_set_id FROM invoicing.invoices WHERE invoice_id = @invoice_id FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
        return await command.ExecuteScalarAsync(cancellationToken) is Guid id ? id : null;
    }

    private static async Task<InvoiceTrace?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid invoiceId,
        Guid sourceSetId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT line_number, transaction_id, allocated_amount
            FROM invoicing.invoice_line_sources
            WHERE invoice_id = @invoice_id
            ORDER BY line_number, allocated_amount DESC, transaction_id;
            """,
            connection,
            transaction);
        command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<InvoiceLineTrace>();
        var sources = new List<InvoiceLineSource>();
        var current = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var lineNumber = reader.GetInt32(0);
            if (lineNumber != current && sources.Count > 0)
            {
                lines.Add(new InvoiceLineTrace(current, sources));
                sources = [];
            }

            current = lineNumber;
            sources.Add(new InvoiceLineSource(reader.GetGuid(1), reader.GetDecimal(2)));
        }

        if (sources.Count == 0)
            return null;
        lines.Add(new InvoiceLineTrace(current, sources));
        return new InvoiceTrace(invoiceId, sourceSetId, lines);
    }

    private static async Task<IReadOnlyList<Line>> ReadLinesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid invoiceId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT line_number, tax_rate, gross_amount
            FROM invoicing.invoice_lines
            WHERE invoice_id = @invoice_id
            ORDER BY line_number;
            """,
            connection,
            transaction);
        command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<Line>();
        while (await reader.ReadAsync(cancellationToken))
            lines.Add(new Line(reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2)));
        return lines;
    }

    // Re-derives the split generation applied and proves it against the stored lines, so a link is never a guess.
    private static async Task<IReadOnlyList<Link>> AllocateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid invoiceId,
        Guid sourceSetId,
        IReadOnlyList<Line> lines,
        CancellationToken cancellationToken)
    {
        var charges = await ReadChargesAsync(connection, transaction, sourceSetId, cancellationToken);
        var unresolved = charges.FirstOrDefault(charge => charge.BillId is null);
        if (unresolved is not null)
            throw new InvoiceTraceMismatchException(
                invoiceId, $"charge {unresolved.TransactionId} does not point at a bill-backed account payment");

        var weights = await ReadBillWeightsAsync(
            connection, transaction, charges.Select(charge => charge.BillId!.Value).Distinct().ToArray(), cancellationToken);
        var lineByRate = lines.ToDictionary(line => line.TaxRate);
        var links = new List<Link>();
        foreach (var charge in charges)
        {
            var split = InvoiceTaxCalculator.SplitByTaxRate(
                    charge.Amount, weights.GetValueOrDefault(charge.BillId!.Value) ?? [])
                ?? throw new InvoiceTraceMismatchException(
                    invoiceId, $"bill {charge.BillId} of charge {charge.TransactionId} has no positive amount at any KDV rate");
            foreach (var (rate, gross) in split)
            {
                if (!lineByRate.TryGetValue(rate, out var line))
                    throw new InvoiceTraceMismatchException(
                        invoiceId, $"charge {charge.TransactionId} has an amount at KDV %{rate} but the invoice has no such line");
                links.Add(new Link(line.LineNumber, charge.TransactionId, gross));
            }
        }

        foreach (var line in lines)
        {
            var linked = links.Where(link => link.LineNumber == line.LineNumber).Sum(link => link.Amount);
            if (linked != line.GrossAmount)
                throw new InvoiceTraceMismatchException(
                    invoiceId, $"line {line.LineNumber} totals {line.GrossAmount} but its charges add up to {linked}");
        }

        return links;
    }

    private static async Task<IReadOnlyList<Charge>> ReadChargesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sourceSetId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT l.transaction_id, l.amount, p.bill_id
            FROM invoicing.invoice_source_lines l
            LEFT JOIN customer_account.account_transactions t ON t.id = l.transaction_id
            LEFT JOIN payments.payments p
                   ON t.source_reference_type = 'Payment' AND p.payment_id = t.source_reference_id
            WHERE l.source_set_id = @source_set_id AND NOT l.cancelled AND l.transaction_type = 'Charge'
            ORDER BY l.occurred_at, l.transaction_id;
            """,
            connection,
            transaction);
        command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var charges = new List<Charge>();
        while (await reader.ReadAsync(cancellationToken))
        {
            charges.Add(new Charge(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetGuid(2)));
        }

        return charges;
    }

    private static async Task<Dictionary<Guid, List<BillTaxWeight>>> ReadBillWeightsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] billIds, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT bill_id, tax_rate, SUM(gross_amount)
            FROM billing.bill_items
            WHERE bill_id = ANY(@bill_ids)
            GROUP BY bill_id, tax_rate
            UNION ALL
            SELECT bill_id, tax_rate, SUM(CASE WHEN is_deduction THEN -gross_amount ELSE gross_amount END)
            FROM billing.bill_adjustments
            WHERE bill_id = ANY(@bill_ids) AND adjustment_type <> 'Tip'
            GROUP BY bill_id, tax_rate;
            """,
            connection,
            transaction);
        command.Parameters.Add("bill_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = billIds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var weights = new Dictionary<Guid, List<BillTaxWeight>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var billId = reader.GetGuid(0);
            if (!weights.TryGetValue(billId, out var list))
                weights[billId] = list = [];
            list.Add(new BillTaxWeight(reader.GetDecimal(1), reader.GetDecimal(2)));
        }

        return weights;
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid invoiceId,
        Guid sourceSetId,
        IReadOnlyList<Link> links,
        Guid recordedBy,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO invoicing.invoice_line_sources
                (invoice_id, line_number, source_set_id, transaction_id, allocated_amount, recorded_by)
            SELECT @invoice_id, link.line_number, @source_set_id, link.transaction_id, link.amount, @recorded_by
            FROM unnest(@line_numbers, @transaction_ids, @amounts) AS link (line_number, transaction_id, amount);
            """,
            connection,
            transaction);
        command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
        command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
        command.Parameters.Add("recorded_by", NpgsqlDbType.Uuid).Value = recordedBy;
        command.Parameters.Add("line_numbers", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
            links.Select(link => link.LineNumber).ToArray();
        command.Parameters.Add("transaction_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            links.Select(link => link.TransactionId).ToArray();
        command.Parameters.Add("amounts", NpgsqlDbType.Array | NpgsqlDbType.Numeric).Value =
            links.Select(link => link.Amount).ToArray();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
