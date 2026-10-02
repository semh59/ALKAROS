using Npgsql;

namespace ALKAROS.Purchasing.PurchaseInvoices;

public sealed class PostgresPurchaseInvoiceRepository : IPurchaseInvoiceRepository
{
    private const int MaxListRows = 500;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPurchaseInvoiceRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(PurchaseInvoice invoice, string rawXml, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await using (var command = new NpgsqlCommand(
                """
                INSERT INTO purchasing.purchase_invoices
                    (invoice_id, ettn, invoice_number, issue_date, supplier_tax_number, supplier_name, supplier_id, currency, source, status, imported_by, raw_xml, created_at, updated_at)
                VALUES (@id, @ettn, @number, @issue_date, @tax, @name, @supplier, @currency, @source, @status, @by, @xml, @created, @created);
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("id", invoice.InvoiceId);
                command.Parameters.AddWithValue("ettn", invoice.Ettn);
                command.Parameters.AddWithValue("number", invoice.InvoiceNumber);
                command.Parameters.AddWithValue("issue_date", invoice.IssueDate);
                command.Parameters.AddWithValue("tax", invoice.SupplierTaxNumber);
                command.Parameters.AddWithValue("name", invoice.SupplierName);
                command.Parameters.AddWithValue("supplier", (object?)invoice.SupplierId ?? DBNull.Value);
                command.Parameters.AddWithValue("currency", invoice.Currency);
                command.Parameters.AddWithValue("source", invoice.Source);
                command.Parameters.AddWithValue("status", invoice.Status);
                command.Parameters.AddWithValue("by", invoice.ImportedBy);
                command.Parameters.AddWithValue("xml", rawXml);
                command.Parameters.AddWithValue("created", invoice.CreatedAt);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var line in invoice.Lines)
            {
                await using var command = new NpgsqlCommand(
                    """
                    INSERT INTO purchasing.purchase_invoice_lines
                        (line_id, invoice_id, line_number, item_key, supplier_item_code, description, quantity, unit_code, unit_price, line_net, stock_item_id, conversion_factor)
                    VALUES (@id, @invoice, @number, @key, @code, @description, @qty, @unit, @price, @net, @stock, @factor);
                    """, connection, transaction);
                command.Parameters.AddWithValue("id", line.LineId);
                command.Parameters.AddWithValue("invoice", invoice.InvoiceId);
                command.Parameters.AddWithValue("number", line.LineNumber);
                command.Parameters.AddWithValue("key", line.ItemKey);
                command.Parameters.AddWithValue("code", (object?)line.SupplierItemCode ?? DBNull.Value);
                command.Parameters.AddWithValue("description", line.Description);
                command.Parameters.AddWithValue("qty", line.Quantity);
                command.Parameters.AddWithValue("unit", line.UnitCode);
                command.Parameters.AddWithValue("price", line.UnitPrice);
                command.Parameters.AddWithValue("net", line.LineNet);
                command.Parameters.AddWithValue("stock", (object?)line.StockItemId ?? DBNull.Value);
                command.Parameters.AddWithValue("factor", (object?)line.ConversionFactor ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_purchase_invoices_ettn" })
        {
            throw new DuplicatePurchaseInvoiceException(invoice.Ettn);
        }
    }

    public async Task<PurchaseInvoice?> GetAsync(Guid invoiceId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        PurchaseInvoice? header = null;
        await using (var command = new NpgsqlCommand(
            """
            SELECT invoice_id, ettn, invoice_number, issue_date, supplier_tax_number, supplier_name, supplier_id, currency, source, status, imported_by, created_at
            FROM purchasing.purchase_invoices WHERE invoice_id = @id;
            """, connection))
        {
            command.Parameters.AddWithValue("id", invoiceId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                header = new PurchaseInvoice(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateOnly>(3),
                    reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetString(7),
                    reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11), []);
            }
        }

        if (header is null)
            return null;

        var lines = new List<PurchaseInvoiceLine>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT line_id, line_number, item_key, supplier_item_code, description, quantity, unit_code, unit_price, line_net, stock_item_id, conversion_factor
            FROM purchasing.purchase_invoice_lines WHERE invoice_id = @id ORDER BY line_number;
            """, connection))
        {
            command.Parameters.AddWithValue("id", invoiceId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                lines.Add(new PurchaseInvoiceLine(
                    reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8),
                    reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetDecimal(10)));
            }
        }

        return header with { Lines = lines };
    }

    public async Task<IReadOnlyList<PurchaseInvoiceSummary>> ListAsync(string? status, CancellationToken ct = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT i.invoice_id, i.invoice_number, i.issue_date, i.supplier_name, i.supplier_id, i.status,
                   COUNT(l.line_id), COUNT(l.line_id) FILTER (WHERE l.stock_item_id IS NULL), COALESCE(SUM(l.line_net), 0)
            FROM purchasing.purchase_invoices i
            LEFT JOIN purchasing.purchase_invoice_lines l ON l.invoice_id = i.invoice_id
            WHERE @status::text IS NULL OR i.status = @status
            GROUP BY i.invoice_id
            ORDER BY i.created_at DESC
            LIMIT @limit;
            """);
        command.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", MaxListRows);

        var result = new List<PurchaseInvoiceSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new PurchaseInvoiceSummary(
                reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetString(5), (int)reader.GetInt64(6), (int)reader.GetInt64(7), reader.GetDecimal(8)));
        }

        return result;
    }

    public async Task<IReadOnlyList<SupplierItemMapping>> FindMappingsAsync(
        string supplierTaxNumber, IReadOnlyCollection<string> itemKeys, CancellationToken ct = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT supplier_tax_number, item_key, purchase_unit_code, stock_item_id, conversion_factor
            FROM purchasing.supplier_item_mappings
            WHERE supplier_tax_number = @tax AND item_key = ANY(@keys)
            LIMIT 5000;
            """);
        command.Parameters.AddWithValue("tax", supplierTaxNumber);
        command.Parameters.AddWithValue("keys", itemKeys.ToArray());

        var result = new List<SupplierItemMapping>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            result.Add(new SupplierItemMapping(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3), reader.GetDecimal(4)));
        return result;
    }

    public async Task<bool> TryTransitionAsync(
        Guid invoiceId, string expectedStatus, string newStatus, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE purchasing.purchase_invoices
            SET status = @to, row_version = row_version + 1, updated_at = now()
            WHERE invoice_id = @id AND status = @from;
            """, connection, transaction);
        command.Parameters.AddWithValue("id", invoiceId);
        command.Parameters.AddWithValue("from", expectedStatus);
        command.Parameters.AddWithValue("to", newStatus);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
    }

    public async Task InsertReceiptAsync(InvoiceReceipt receipt, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
    {
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO purchasing.goods_receipts
                (receipt_id, receipt_number, order_id, invoice_id, supplier_id, destination_location_id, received_at, received_by, approved_by, notes)
            VALUES (@id, @number, NULL, @invoice, @supplier, @location, @received_at, @by, @by, @notes);
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("id", receipt.ReceiptId);
            command.Parameters.AddWithValue("number", receipt.ReceiptNumber);
            command.Parameters.AddWithValue("invoice", receipt.InvoiceId);
            command.Parameters.AddWithValue("supplier", receipt.SupplierId);
            command.Parameters.AddWithValue("location", receipt.LocationId);
            command.Parameters.AddWithValue("received_at", receipt.ReceivedAt);
            command.Parameters.AddWithValue("by", receipt.ReceivedBy);
            command.Parameters.AddWithValue("notes", receipt.Notes);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        foreach (var line in receipt.Lines)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO purchasing.goods_receipt_items
                    (item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, unit_code, unit_price, is_approved_by_manager)
                VALUES (@id, @receipt, NULL, @stock, @qty, @qty, @unit, @price, true);
                """, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("receipt", receipt.ReceiptId);
            command.Parameters.AddWithValue("stock", line.StockItemId);
            command.Parameters.AddWithValue("qty", line.Quantity);
            command.Parameters.AddWithValue("unit", line.UnitCode);
            command.Parameters.AddWithValue("price", line.UnitPrice);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<int> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        string tax, key, unit;
        await using (var command = new NpgsqlCommand(
            """
            SELECT i.status, i.supplier_tax_number, l.item_key, l.unit_code
            FROM purchasing.purchase_invoices i
            JOIN purchasing.purchase_invoice_lines l ON l.invoice_id = i.invoice_id AND l.line_id = @line
            WHERE i.invoice_id = @invoice
            FOR UPDATE OF i;
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("invoice", invoiceId);
            command.Parameters.AddWithValue("line", lineId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                throw new PurchaseInvoiceNotFoundException(invoiceId);
            if (reader.GetString(0) != PurchaseInvoiceStatuses.Draft)
                throw new PurchaseInvoiceStatusException("Only a draft purchase invoice can be mapped.");
            tax = reader.GetString(1);
            key = reader.GetString(2);
            unit = reader.GetString(3);
        }

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO purchasing.supplier_item_mappings (mapping_id, supplier_tax_number, item_key, purchase_unit_code, stock_item_id, conversion_factor)
            VALUES (@id, @tax, @key, @unit, @stock, @factor)
            ON CONFLICT (supplier_tax_number, item_key, purchase_unit_code)
            DO UPDATE SET stock_item_id = EXCLUDED.stock_item_id, conversion_factor = EXCLUDED.conversion_factor, updated_at = now();
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("tax", tax);
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("unit", unit);
            command.Parameters.AddWithValue("stock", stockItemId);
            command.Parameters.AddWithValue("factor", conversionFactor);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        int mapped;
        await using (var command = new NpgsqlCommand(
            """
            UPDATE purchasing.purchase_invoice_lines l
            SET stock_item_id = @stock, conversion_factor = @factor
            FROM purchasing.purchase_invoices i
            WHERE i.invoice_id = l.invoice_id AND i.status = 'Draft' AND i.supplier_tax_number = @tax
              AND l.item_key = @key AND l.unit_code = @unit;
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("stock", stockItemId);
            command.Parameters.AddWithValue("factor", conversionFactor);
            command.Parameters.AddWithValue("tax", tax);
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("unit", unit);
            mapped = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return mapped;
    }
}
