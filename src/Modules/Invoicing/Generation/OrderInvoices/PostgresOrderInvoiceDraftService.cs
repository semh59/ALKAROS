using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Invoicing.Generation.OrderInvoices;

/// <summary>
/// Against <c>invoicing.order_invoices</c> / <c>order_invoice_lines</c> (migration 171). The unique index on the
/// order makes drafting idempotent: a concurrent or repeated call finds the winner's row instead of adding a second.
/// </summary>
public sealed class PostgresOrderInvoiceDraftService(NpgsqlDataSource dataSource, ISellerProfileStore sellers)
    : IOrderInvoiceDraftService
{
    private const string UnitCode = "C62";
    private static readonly string[] Providers = ["yemeksepeti", "trendyol-go"];

    private readonly NpgsqlDataSource _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    private readonly ISellerProfileStore _sellers = sellers ?? throw new ArgumentNullException(nameof(sellers));

    public async Task<OrderInvoiceDraftResult> CreateAsync(
        OrderInvoiceInput input, Guid? createdBy, CancellationToken cancellationToken = default)
    {
        Validate(input);

        if (await GetByOrderAsync(input.OrderId, cancellationToken) is { } existing)
            return new OrderInvoiceDraftResult(existing, true);

        var seller = await _sellers.GetAsync(cancellationToken);
        if (seller is null || seller.Problems().Count > 0)
            throw new SellerProfileMissingException();

        var invoiceId = Guid.NewGuid();
        var net = input.Lines.Sum(line => line.NetAmount);
        var tax = input.Lines.Sum(line => line.TaxAmount);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var header = new NpgsqlCommand(
            """
            INSERT INTO invoicing.order_invoices
                (invoice_id, order_id, provider, external_order_id, order_number, profile, ubl_profile_id,
                 invoice_type_code, currency_code, issue_date, service_date, status, buyer_kind,
                 seller_legal_name, seller_tax_id_kind, seller_tax_id_number, seller_tax_office, seller_address,
                 web_address, payment_method, payment_date, carrier_name, carrier_tax_id,
                 line_extension_amount, tax_total, payable_amount, created_by)
            VALUES
                (@invoice_id, @order_id, @provider, @external_order_id, @order_number, 'EArsiv', 'EARSIVFATURA',
                 'SATIS', 'TRY', (now() AT TIME ZONE 'Europe/Istanbul')::date, @service_date, 'Draft', 'FinalConsumer',
                 @seller_name, @seller_kind, @seller_number, @seller_office, @seller_address,
                 @web_address, @payment_method, @payment_date, @carrier_name, @carrier_tax_id,
                 @net, @tax, @gross, @created_by)
            ON CONFLICT (order_id) DO NOTHING;
            """, connection, transaction))
        {
            header.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
            header.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = input.OrderId;
            header.Parameters.AddWithValue("provider", input.Provider);
            header.Parameters.AddWithValue("external_order_id", input.ExternalOrderId);
            header.Parameters.AddWithValue("order_number", input.OrderNumber);
            header.Parameters.Add("service_date", NpgsqlDbType.Date).Value = input.ServiceDate;
            header.Parameters.AddWithValue("seller_name", seller.LegalName.Trim());
            header.Parameters.AddWithValue("seller_kind", seller.TaxIdKind);
            header.Parameters.AddWithValue("seller_number", seller.TaxIdNumber);
            header.Parameters.AddWithValue("seller_office", seller.TaxOffice.Trim());
            header.Parameters.AddWithValue("seller_address", $"{seller.Address.Trim()}, {seller.District.Trim()}/{seller.City.Trim()}");
            header.Parameters.AddWithValue("web_address", input.WebAddress);
            header.Parameters.Add("payment_method", NpgsqlDbType.Text).Value = (object?)input.PaymentMethod ?? DBNull.Value;
            header.Parameters.Add("payment_date", NpgsqlDbType.Date).Value = (object?)input.PaymentDate ?? DBNull.Value;
            header.Parameters.Add("carrier_name", NpgsqlDbType.Text).Value = (object?)input.CarrierName ?? DBNull.Value;
            header.Parameters.Add("carrier_tax_id", NpgsqlDbType.Text).Value = (object?)input.CarrierTaxId ?? DBNull.Value;
            header.Parameters.AddWithValue("net", net);
            header.Parameters.AddWithValue("tax", tax);
            header.Parameters.AddWithValue("gross", net + tax);
            header.Parameters.Add("created_by", NpgsqlDbType.Uuid).Value = (object?)createdBy ?? DBNull.Value;
            if (await header.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                var winner = await GetByOrderAsync(input.OrderId, cancellationToken)
                    ?? throw new InvalidOperationException($"Order {input.OrderId} has an invoice conflict but no invoice.");
                return new OrderInvoiceDraftResult(winner, true);
            }
        }

        var number = 0;
        foreach (var line in input.Lines)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO invoicing.order_invoice_lines
                    (invoice_id, line_number, description, quantity, unit_code, tax_rate, net_amount, tax_amount, gross_amount)
                VALUES (@invoice_id, @line_number, @description, @quantity, @unit_code, @tax_rate, @net, @tax, @gross);
                """, connection, transaction);
            command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
            command.Parameters.AddWithValue("line_number", ++number);
            command.Parameters.AddWithValue("description", line.Description.Trim());
            command.Parameters.AddWithValue("quantity", line.Quantity);
            command.Parameters.AddWithValue("unit_code", UnitCode);
            command.Parameters.AddWithValue("tax_rate", line.TaxRate);
            command.Parameters.AddWithValue("net", line.NetAmount);
            command.Parameters.AddWithValue("tax", line.TaxAmount);
            command.Parameters.AddWithValue("gross", line.GrossAmount);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var created = await GetByOrderAsync(input.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found after it was drafted.");
        return new OrderInvoiceDraftResult(created, false);
    }

    public async Task<OrderInvoiceDraft?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        OrderInvoiceDraft? draft;
        await using (var command = new NpgsqlCommand(
            """
            SELECT invoice_id, order_id, provider, external_order_id, order_number, issue_date, service_date, status,
                   seller_legal_name, seller_tax_id_kind, seller_tax_id_number, seller_tax_office, seller_address,
                   web_address, payment_method, payment_date, carrier_name, carrier_tax_id,
                   line_extension_amount, tax_total, payable_amount, created_at, created_by
            FROM invoicing.order_invoices
            WHERE order_id = @order_id;
            """, connection))
        {
            command.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            draft = new OrderInvoiceDraft(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                DateOnly.FromDateTime(reader.GetDateTime(5)), DateOnly.FromDateTime(reader.GetDateTime(6)),
                Enum.Parse<InvoiceStatus>(reader.GetString(7)),
                new OrderInvoiceSeller(
                    reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12)),
                reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : DateOnly.FromDateTime(reader.GetDateTime(15)),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.IsDBNull(17) ? null : reader.GetString(17),
                reader.GetDecimal(18), reader.GetDecimal(19), reader.GetDecimal(20),
                reader.GetFieldValue<DateTimeOffset>(21),
                reader.IsDBNull(22) ? null : reader.GetGuid(22),
                []);
        }

        var lines = new List<OrderInvoiceLine>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT line_number, description, quantity, unit_code, tax_rate, net_amount, tax_amount, gross_amount
            FROM invoicing.order_invoice_lines
            WHERE invoice_id = @invoice_id
            ORDER BY line_number;
            """, connection))
        {
            command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = draft.InvoiceId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                lines.Add(new OrderInvoiceLine(
                    reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3),
                    reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7)));
        }

        return draft with { Lines = lines };
    }

    private static void Validate(OrderInvoiceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.OrderId == Guid.Empty)
            throw new ArgumentException("Order id cannot be empty.", nameof(input));
        if (!Providers.Contains(input.Provider))
            throw new ArgumentException($"Unknown provider '{input.Provider}'.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.ExternalOrderId) || string.IsNullOrWhiteSpace(input.OrderNumber)
            || string.IsNullOrWhiteSpace(input.WebAddress))
            throw new ArgumentException("External order id, order number and web address are required.", nameof(input));
        if (input.Lines is null || input.Lines.Count == 0)
            throw new OrderInvoiceNothingToInvoiceException(input.OrderId);
        foreach (var line in input.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0 || line.TaxRate < 0
                || line.NetAmount < 0 || line.TaxAmount < 0 || line.GrossAmount <= 0
                || line.NetAmount + line.TaxAmount != line.GrossAmount)
                throw new ArgumentException(
                    $"Invoice line '{line.Description}' is not a positive tax-inclusive amount (net + tax = gross).", nameof(input));
        }
    }
}
