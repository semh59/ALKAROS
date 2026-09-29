using System.Globalization;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Invoicing.Generation;

/// <summary>
/// V14-INV-002 against <c>invoicing.invoices</c> / <c>invoicing.invoice_lines</c> (migration 168). The source set
/// row is locked for the whole generation, so two concurrent calls produce one invoice; the partial unique index on
/// the set backs that up. Charges resolve to their bill through the account-charge payment
/// (<c>source_reference_type = 'Payment'</c>, V14-ACC-003) and are split by that bill's KDV rates: its items plus
/// its fee and discount adjustments (tips carry no KDV and are not a weight). The buyer snapshot is sealed in an
/// AES-256-GCM envelope with the same master key as the customer profile.
/// </summary>
public sealed class PostgresInvoiceGenerationService : IInvoiceGenerationService
{
    private const string UniqueViolation = "23505";
    private const string ChargeTransactionType = "Charge";
    private const string ChargeReferenceType = "Payment";
    private const string InvoiceTypeCode = "SATIS";
    private const string CurrencyCode = "TRY";
    private const string UnitCode = "C62";
    private static readonly SecretReference MasterKey = new("envelope-master-key");
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private const string NameField = "name";
    private const string TaxIdKindField = "tax_id_kind";
    private const string TaxIdNumberField = "tax_id_number";
    private const string TaxOfficeField = "tax_office";
    private const string AddressField = "address";
    private const string EmailField = "email";

    private readonly NpgsqlDataSource _dataSource;
    private readonly ICustomerProfileStore _profiles;
    private readonly SensitivePayloadProtector _protector;

    public PostgresInvoiceGenerationService(
        NpgsqlDataSource dataSource, ICustomerProfileStore profiles, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        ArgumentNullException.ThrowIfNull(secretProvider);

        var policy = new InvoiceBuyerEncryptionPolicy();
        _protector = new SensitivePayloadProtector(
            new AesGcmEnvelopeCipher(new SecretResolver(secretProvider, policy)), policy);
    }

    /// <summary>GIB profile identifiers: e-Fatura goes out as a basic invoice, e-Arsiv as its own profile.</summary>
    public static string UblProfileIdFor(InvoiceProfile profile) => profile switch
    {
        InvoiceProfile.EFatura => "TEMELFATURA",
        InvoiceProfile.EArsiv => "EARSIVFATURA",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown invoice profile."),
    };

    public async Task<InvoiceGenerationResult> GenerateAsync(
        Guid sourceSetId, InvoiceProfile profile, Guid generatedBy, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(profile))
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown invoice profile.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var set = await LockSourceSetAsync(connection, transaction, sourceSetId, cancellationToken)
            ?? throw new InvoiceGenerationSourceSetNotFoundException(sourceSetId);
        var existingId = await FindInvoiceIdAsync(connection, transaction, sourceSetId, cancellationToken);
        if (existingId is { } already)
        {
            await transaction.CommitAsync(cancellationToken);
            return ExistingResult(await GetAsync(already, cancellationToken) ?? throw MissingInvoice(already), profile);
        }

        if (set.Cancelled)
            throw new InvoiceGenerationSourceSetCancelledException(sourceSetId);

        var charges = await ReadChargesAsync(connection, transaction, sourceSetId, cancellationToken);
        if (charges.Count == 0)
            throw new InvoiceGenerationNothingToInvoiceException(sourceSetId);

        var grossByRate = await SplitChargesAsync(connection, transaction, charges, cancellationToken);
        var groups = InvoiceTaxCalculator.Groups(grossByRate);
        var charged = charges.Sum(charge => charge.Amount);
        if (groups.Sum(group => group.GrossAmount) != charged)
            throw new InvalidOperationException(
                $"Invoice groups of source set {sourceSetId} total {groups.Sum(group => group.GrossAmount)}, charges total {charged}.");

        var buyer = await ReadBuyerAsync(set.CustomerId, cancellationToken);
        var invoiceId = Guid.NewGuid();
        var lines = groups
            .Select((group, index) => new InvoiceDraftLine(
                index + 1,
                Describe(set.PeriodStart, set.PeriodEnd, group.TaxRate),
                1m,
                UnitCode,
                group.TaxRate,
                group.NetAmount,
                group.TaxAmount,
                group.GrossAmount))
            .ToList();

        try
        {
            await InsertAsync(connection, transaction, invoiceId, sourceSetId, set, profile, buyer, lines, generatedBy, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
        {
            // Defense in depth: the set row lock already serializes generation for one set.
            await transaction.RollbackAsync(cancellationToken);
            var winner = await GetBySourceSetAsync(sourceSetId, cancellationToken);
            if (winner is null)
                throw;
            return ExistingResult(winner, profile);
        }

        return new InvoiceGenerationResult(await GetAsync(invoiceId, cancellationToken) ?? throw MissingInvoice(invoiceId), false);
    }

    public async Task<InvoiceDraft?> GetAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"{SelectInvoice} WHERE i.invoice_id = @invoice_id;");
        command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
        return await ReadInvoiceAsync(command, cancellationToken);
    }

    public async Task<InvoiceDraft?> GetBySourceSetAsync(Guid sourceSetId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"{SelectInvoice} WHERE i.source_set_id = @source_set_id;");
        command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
        return await ReadInvoiceAsync(command, cancellationToken);
    }

    private const string SelectInvoice =
        """
        SELECT i.invoice_id, i.source_set_id, i.period_id, i.customer_id, i.profile, i.ubl_profile_id,
               i.invoice_type_code, i.currency_code, i.issue_date, i.status, i.buyer_envelope,
               i.line_extension_amount, i.tax_total, i.payable_amount, i.created_at, i.created_by
        FROM invoicing.invoices i
        """;

    private static InvoiceGenerationResult ExistingResult(InvoiceDraft existing, InvoiceProfile requested)
        => existing.Profile == requested
            ? new InvoiceGenerationResult(existing, WasAlreadyGenerated: true)
            : throw new InvoiceAlreadyGeneratedAsAnotherProfileException(existing.SourceSetId, existing.Profile);

    private static InvalidOperationException MissingInvoice(Guid invoiceId)
        => new($"Invoice {invoiceId} vanished right after it was read.");

    private static string Describe(DateOnly periodStart, DateOnly periodEnd, decimal taxRate)
        => string.Format(
            Turkish,
            "Restoran hizmet bedeli, {0:dd.MM.yyyy}-{1:dd.MM.yyyy} dönemi (KDV %{2:0.##})",
            periodStart,
            periodEnd.AddDays(-1),
            taxRate);

    private sealed record LockedSourceSet(Guid PeriodId, Guid CustomerId, bool Cancelled, DateOnly PeriodStart, DateOnly PeriodEnd);

    private sealed record Charge(Guid TransactionId, decimal Amount, Guid? BillId, string? ReferenceType);

    private static async Task<LockedSourceSet?> LockSourceSetAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sourceSetId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT s.period_id, s.customer_id, s.status, p.period_start, p.period_end
            FROM invoicing.invoice_source_sets s
            JOIN invoicing.invoice_periods p ON p.period_id = s.period_id
            WHERE s.source_set_id = @source_set_id
            FOR UPDATE OF s;
            """,
            connection,
            transaction);
        command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new LockedSourceSet(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2) == "Cancelled",
            reader.GetFieldValue<DateOnly>(3),
            reader.GetFieldValue<DateOnly>(4));
    }

    private static async Task<Guid?> FindInvoiceIdAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sourceSetId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT invoice_id FROM invoicing.invoices WHERE source_set_id = @source_set_id;", connection, transaction);
        command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
        return await command.ExecuteScalarAsync(cancellationToken) is Guid id ? id : null;
    }

    private static async Task<IReadOnlyList<Charge>> ReadChargesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sourceSetId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT l.transaction_id, l.amount, p.bill_id, t.source_reference_type
            FROM invoicing.invoice_source_lines l
            LEFT JOIN customer_account.account_transactions t ON t.id = l.transaction_id
            LEFT JOIN payments.payments p
                   ON t.source_reference_type = @reference_type AND p.payment_id = t.source_reference_id
            WHERE l.source_set_id = @source_set_id AND NOT l.cancelled AND l.transaction_type = @charge
            ORDER BY l.occurred_at, l.transaction_id;
            """,
            connection,
            transaction);
        command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
        command.Parameters.Add("reference_type", NpgsqlDbType.Text).Value = ChargeReferenceType;
        command.Parameters.Add("charge", NpgsqlDbType.Text).Value = ChargeTransactionType;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var charges = new List<Charge>();
        while (await reader.ReadAsync(cancellationToken))
        {
            charges.Add(new Charge(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetGuid(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3)));
        }

        return charges;
    }

    private static async Task<IReadOnlyDictionary<decimal, decimal>> SplitChargesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<Charge> charges,
        CancellationToken cancellationToken)
    {
        foreach (var charge in charges.Where(charge => charge.BillId is null))
        {
            throw new InvoiceChargeSourceUnresolvedException(
                charge.TransactionId,
                charge.ReferenceType is null
                    ? "the ledger transaction is missing"
                    : $"its source ({charge.ReferenceType}) is not an account-charge payment with a bill");
        }

        var billIds = charges.Select(charge => charge.BillId!.Value).Distinct().ToArray();
        var weights = await ReadBillWeightsAsync(connection, transaction, billIds, cancellationToken);
        var grossByRate = new Dictionary<decimal, decimal>();
        foreach (var charge in charges)
        {
            var split = InvoiceTaxCalculator.SplitByTaxRate(
                    charge.Amount,
                    weights.TryGetValue(charge.BillId!.Value, out var billWeights) ? billWeights : [])
                ?? throw new InvoiceChargeSourceUnresolvedException(
                    charge.TransactionId, $"bill {charge.BillId} has no positive amount at any KDV rate");
            foreach (var (rate, gross) in split)
                grossByRate[rate] = grossByRate.GetValueOrDefault(rate) + gross;
        }

        return grossByRate;
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

    private async Task<InvoiceBuyerSnapshot> ReadBuyerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        // Invoice data is Manager/Finance (V0-CMP-003): the full tax number is read here and nowhere on the till.
        var profile = await _profiles.GetAsync(customerId, CustomerAccessRole.Manager, cancellationToken)
            ?? throw new InvoiceBuyerIncompleteException(customerId, InvoiceBuyerProblem.CustomerNotFound);
        if (profile.Anonymized)
            throw new InvoiceBuyerIncompleteException(customerId, InvoiceBuyerProblem.CustomerAnonymized);
        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new InvoiceBuyerIncompleteException(customerId, InvoiceBuyerProblem.NameMissing);
        if (profile.TaxIdentity is null)
            throw new InvoiceBuyerIncompleteException(customerId, InvoiceBuyerProblem.TaxIdentityMissing);

        return new InvoiceBuyerSnapshot(
            profile.Name.Trim(),
            profile.TaxIdentity.Kind.ToString(),
            profile.TaxIdentity.Number,
            profile.TaxIdentity.TaxOffice,
            profile.Address,
            profile.Email);
    }

    private async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid invoiceId,
        Guid sourceSetId,
        LockedSourceSet set,
        InvoiceProfile profile,
        InvoiceBuyerSnapshot buyer,
        IReadOnlyList<InvoiceDraftLine> lines,
        Guid generatedBy,
        CancellationToken cancellationToken)
    {
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO invoicing.invoices
                (invoice_id, source_set_id, period_id, customer_id, profile, ubl_profile_id, invoice_type_code,
                 currency_code, issue_date, status, buyer_envelope, line_extension_amount, tax_total, payable_amount,
                 created_by)
            VALUES (@invoice_id, @source_set_id, @period_id, @customer_id, @profile, @ubl_profile_id, @invoice_type_code,
                    @currency_code, (now() AT TIME ZONE 'Europe/Istanbul')::date, 'Draft', @buyer_envelope,
                    @line_extension_amount, @tax_total, @payable_amount, @created_by);
            """,
            connection,
            transaction))
        {
            command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
            command.Parameters.Add("source_set_id", NpgsqlDbType.Uuid).Value = sourceSetId;
            command.Parameters.Add("period_id", NpgsqlDbType.Uuid).Value = set.PeriodId;
            command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = set.CustomerId;
            command.Parameters.Add("profile", NpgsqlDbType.Text).Value = profile.ToString();
            command.Parameters.Add("ubl_profile_id", NpgsqlDbType.Text).Value = UblProfileIdFor(profile);
            command.Parameters.Add("invoice_type_code", NpgsqlDbType.Text).Value = InvoiceTypeCode;
            command.Parameters.Add("currency_code", NpgsqlDbType.Char).Value = CurrencyCode;
            command.Parameters.Add("buyer_envelope", NpgsqlDbType.Bytea).Value = ProtectBuyer(buyer).ToPersistenceBytes();
            command.Parameters.Add("line_extension_amount", NpgsqlDbType.Numeric).Value = lines.Sum(line => line.NetAmount);
            command.Parameters.Add("tax_total", NpgsqlDbType.Numeric).Value = lines.Sum(line => line.TaxAmount);
            command.Parameters.Add("payable_amount", NpgsqlDbType.Numeric).Value = lines.Sum(line => line.GrossAmount);
            command.Parameters.Add("created_by", NpgsqlDbType.Uuid).Value = generatedBy;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var line in lines)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO invoicing.invoice_lines
                    (invoice_id, line_number, description, quantity, unit_code, tax_rate, net_amount, tax_amount, gross_amount)
                VALUES (@invoice_id, @line_number, @description, @quantity, @unit_code, @tax_rate, @net_amount, @tax_amount,
                        @gross_amount);
                """,
                connection,
                transaction);
            command.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoiceId;
            command.Parameters.Add("line_number", NpgsqlDbType.Integer).Value = line.LineNumber;
            command.Parameters.Add("description", NpgsqlDbType.Text).Value = line.Description;
            command.Parameters.Add("quantity", NpgsqlDbType.Numeric).Value = line.Quantity;
            command.Parameters.Add("unit_code", NpgsqlDbType.Text).Value = line.UnitCode;
            command.Parameters.Add("tax_rate", NpgsqlDbType.Numeric).Value = line.TaxRate;
            command.Parameters.Add("net_amount", NpgsqlDbType.Numeric).Value = line.NetAmount;
            command.Parameters.Add("tax_amount", NpgsqlDbType.Numeric).Value = line.TaxAmount;
            command.Parameters.Add("gross_amount", NpgsqlDbType.Numeric).Value = line.GrossAmount;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<InvoiceDraft?> ReadInvoiceAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        InvoiceDraft? invoice;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            invoice = new InvoiceDraft(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                Enum.Parse<InvoiceProfile>(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetFieldValue<DateOnly>(8),
                Enum.Parse<InvoiceStatus>(reader.GetString(9)),
                UnprotectBuyer((byte[])reader[10]),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                reader.GetDecimal(13),
                reader.GetFieldValue<DateTimeOffset>(14),
                reader.GetGuid(15),
                []);
        }

        await using var lines = _dataSource.CreateCommand(
            """
            SELECT line_number, description, quantity, unit_code, tax_rate, net_amount, tax_amount, gross_amount
            FROM invoicing.invoice_lines
            WHERE invoice_id = @invoice_id
            ORDER BY line_number;
            """);
        lines.Parameters.Add("invoice_id", NpgsqlDbType.Uuid).Value = invoice.InvoiceId;
        await using var lineReader = await lines.ExecuteReaderAsync(cancellationToken);
        var read = new List<InvoiceDraftLine>();
        while (await lineReader.ReadAsync(cancellationToken))
        {
            read.Add(new InvoiceDraftLine(
                lineReader.GetInt32(0),
                lineReader.GetString(1),
                lineReader.GetDecimal(2),
                lineReader.GetString(3),
                lineReader.GetDecimal(4),
                lineReader.GetDecimal(5),
                lineReader.GetDecimal(6),
                lineReader.GetDecimal(7)));
        }

        return invoice with { Lines = read };
    }

    private SensitiveEnvelope ProtectBuyer(InvoiceBuyerSnapshot buyer)
    {
        var fields = new Dictionary<string, string>
        {
            [NameField] = buyer.Name,
            [TaxIdKindField] = buyer.TaxIdKind,
            [TaxIdNumberField] = buyer.TaxIdNumber,
        };
        if (buyer.TaxOffice is not null)
            fields[TaxOfficeField] = buyer.TaxOffice;
        if (buyer.Address is not null)
            fields[AddressField] = buyer.Address;
        if (buyer.Email is not null)
            fields[EmailField] = buyer.Email;

        var categories = fields.Keys.ToDictionary(key => key, _ => SensitiveCategory.Pii);
        return _protector.Protect(new SensitivePayload(fields, categories), MasterKey, InvoiceBuyerEncryptionPolicy.Accessor);
    }

    private InvoiceBuyerSnapshot UnprotectBuyer(byte[] envelopeBytes)
    {
        var payload = _protector.Unprotect(
            SensitiveEnvelope.FromPersistenceBytes(envelopeBytes), MasterKey, InvoiceBuyerEncryptionPolicy.Accessor);
        var fields = payload.Fields;
        return new InvoiceBuyerSnapshot(
            fields[NameField],
            fields[TaxIdKindField],
            fields[TaxIdNumberField],
            fields.GetValueOrDefault(TaxOfficeField),
            fields.GetValueOrDefault(AddressField),
            fields.GetValueOrDefault(EmailField));
    }
}
