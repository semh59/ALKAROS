using System.Globalization;
using ALKAROS.CustomerAccounts.AccountReceipts;
using ALKAROS.CustomerAccounts.TransactionLedger;
using ALKAROS.CustomerData.Profiles;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.CustomerAccounts;

/// <summary>
/// V1-RMD-442: the till's read model over customers and their accounts. Contact details are stored encrypted
/// (V14-CST-001), so names and phones are decrypted per customer through <see cref="ICustomerProfileStore"/> with the
/// Cashier role; the phone is masked to its last four digits before it leaves the server, and the tax number
/// (V1-RMD-453) arrives already masked by the profile access policy.
/// </summary>
public sealed class CustomerAccountsStore
{
    /// <summary>Upper bound on the customers the till lists; search filters within it.</summary>
    public const int MaxListed = 500;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly NpgsqlDataSource _dataSource;
    private readonly ICustomerProfileStore _profiles;
    private readonly IAccountTransactionLedger _ledger;
    private readonly IAccountReceiptService _receipts;

    public CustomerAccountsStore(
        NpgsqlDataSource dataSource,
        ICustomerProfileStore profiles,
        IAccountTransactionLedger ledger,
        IAccountReceiptService receipts)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    }

    public async Task<IReadOnlyList<CustomerAccountSummaryV1>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var rows = await ReadAccountRowsAsync(null, cancellationToken);
        var term = search?.Trim();
        var digits = term is null ? string.Empty : new string(term.Where(char.IsDigit).ToArray());
        var result = new List<CustomerAccountSummaryV1>();
        foreach (var row in rows)
        {
            var profile = await _profiles.GetAsync(row.CustomerId, CustomerAccessRole.Cashier, cancellationToken);
            if (profile is null || profile.Anonymized)
                continue;
            if (!string.IsNullOrEmpty(term)
                && Turkish.CompareInfo.IndexOf(profile.Name ?? string.Empty, term, CompareOptions.IgnoreCase) < 0
                && !(digits.Length >= 3 && Digits(profile.Phone).Contains(digits, StringComparison.Ordinal)))
                continue;
            result.Add(Summary(row, profile));
        }

        return result.OrderBy(summary => summary.Name, StringComparer.Create(Turkish, ignoreCase: true)).ToArray();
    }

    public async Task<CustomerAccountSummaryV1?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var row = (await ReadAccountRowsAsync(customerId, cancellationToken)).SingleOrDefault();
        if (row is null)
            return null;
        var profile = await _profiles.GetAsync(customerId, CustomerAccessRole.Cashier, cancellationToken);
        return profile is null || profile.Anonymized ? null : Summary(row, profile);
    }

    public async Task<CustomerAccountSummaryV1> CreateAsync(CreateCustomerV1 request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
            throw new CustomerAccountValidationException("Müşteri adı gerekli (en çok 120 karakter).");
        var phone = request.Phone?.Trim();
        if (!string.IsNullOrEmpty(phone) && (Digits(phone).Length is < 7 or > 15 || phone.Length > 25))
            throw new CustomerAccountValidationException("Telefon numarası 7-15 rakam olmalı.");
        var taxIdentity = ParseTaxIdentity(request.TaxIdKind, request.TaxIdNumber, request.TaxOffice);

        var customerId = await _profiles.CreateAsync(
            new CreateCustomerProfileRequest(name, string.IsNullOrEmpty(phone) ? null : phone, null, null, taxIdentity),
            cancellationToken);
        return await GetAsync(customerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer {customerId} vanished right after it was created.");
    }

    /// <summary>
    /// V1-RMD-453: replaces the customer's tax identity and keeps every other field. Reads the full profile with the
    /// Manager role on the server only, because the update rewrites the whole envelope; the answer is the masked
    /// summary. Returns null for an unknown or anonymized customer.
    /// </summary>
    public async Task<CustomerAccountSummaryV1?> UpdateTaxIdentityAsync(
        Guid customerId, UpdateCustomerTaxIdentityV1 request, CancellationToken cancellationToken)
    {
        var taxIdentity = ParseTaxIdentity(request.TaxIdKind, request.TaxIdNumber, request.TaxOffice);
        var profile = await _profiles.GetAsync(customerId, CustomerAccessRole.Manager, cancellationToken);
        if (profile is null || profile.Anonymized)
            return null;

        try
        {
            await _profiles.UpdateContactAsync(
                customerId,
                new UpdateCustomerContactRequest(profile.Name, profile.Phone, profile.Email, profile.Address, taxIdentity),
                profile.RowVersion,
                cancellationToken);
        }
        catch (CustomerProfileAnonymizedException)
        {
            return null;
        }

        return await GetAsync(customerId, cancellationToken);
    }

    /// <summary>No kind, number or office means "no tax identity"; anything else must be a complete, valid one.</summary>
    public static CustomerTaxIdentity? ParseTaxIdentity(string? kindText, string? number, string? taxOffice)
    {
        if (string.IsNullOrWhiteSpace(kindText) && string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(taxOffice))
            return null;
        var kindName = kindText?.Trim() ?? string.Empty;
        if (!kindName.All(char.IsAsciiLetter)
            || !Enum.TryParse<CustomerTaxIdKind>(kindName, ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
            throw new CustomerAccountValidationException("Vergi kimlik türünü seçin (VKN ya da TCKN).");
        if (string.IsNullOrWhiteSpace(number))
            throw new CustomerAccountValidationException(kind == CustomerTaxIdKind.Vkn
                ? "Vergi kimlik numarası gerekli."
                : "T.C. kimlik numarası gerekli.");

        try
        {
            return CustomerTaxIdentity.Create(kind, number, taxOffice);
        }
        catch (InvalidCustomerTaxIdentityException exception)
        {
            throw new CustomerAccountValidationException(exception.Error switch
            {
                CustomerTaxIdentityError.VknLength => "Vergi kimlik numarası 10 rakam olmalı.",
                CustomerTaxIdentityError.VknChecksum => "Vergi kimlik numarası geçersiz; rakamları kontrol edin.",
                CustomerTaxIdentityError.TcknLength => "T.C. kimlik numarası 11 rakam olmalı ve 0 ile başlamamalı.",
                CustomerTaxIdentityError.TcknChecksum => "T.C. kimlik numarası geçersiz; rakamları kontrol edin.",
                CustomerTaxIdentityError.TaxOfficeRequired => "Vergi kimlik numarası için vergi dairesi gerekli.",
                CustomerTaxIdentityError.TaxOfficeTooLong =>
                    $"Vergi dairesi en çok {CustomerTaxIdentity.TaxOfficeMaxLength} karakter olabilir.",
                _ => "Vergi kimliği geçersiz.",
            });
        }
    }

    public async Task<CustomerStatementV1?> GetStatementAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var summary = await GetAsync(customerId, cancellationToken);
        if (summary is null)
            return null;

        var entries = (await _ledger.GetByCustomerAsync(customerId, limit: 200, cancellationToken))
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => new CustomerStatementEntryV1(
                entry.Id, entry.OccurredAt, entry.TransactionType.ToString(), Describe(entry.TransactionType),
                entry.Amount, entry.SignedBalanceEffect))
            .ToArray();
        var receipts = (await _receipts.GetByCustomerAsync(customerId, limit: 100, cancellationToken))
            .Select(receipt => new CustomerReceiptV1(receipt.ReceiptNumber, receipt.AccountPaymentId, receipt.Amount, receipt.IssuedAt))
            .ToArray();
        return new CustomerStatementV1(summary, entries, receipts);
    }

    public static string? MaskPhone(string? phone)
    {
        var digits = Digits(phone);
        if (digits.Length == 0)
            return null;
        return digits.Length <= 4 ? new string('*', digits.Length) : new string('*', digits.Length - 4) + digits[^4..];
    }

    private static string Digits(string? value) => value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    private static string Describe(AccountTransactionType type) => type switch
    {
        AccountTransactionType.Charge => "Hesaba yazılan adisyon",
        AccountTransactionType.Payment => "Tahsilat",
        AccountTransactionType.Invoice => "Fatura",
        AccountTransactionType.Credit => "Alacak kaydı",
        AccountTransactionType.Debit => "Borç kaydı",
        AccountTransactionType.Adjustment => "Düzeltme",
        AccountTransactionType.Refund => "İade",
        _ => "Hareket",
    };

    private static CustomerAccountSummaryV1 Summary(AccountRow row, CustomerProfile profile)
        => new(
            row.CustomerId,
            profile.Name ?? "İsimsiz müşteri",
            MaskPhone(profile.Phone),
            row.Balance,
            row.CreditLimit,
            Math.Max(0m, row.CreditLimit - row.Balance),
            row.PaymentTermDays,
            profile.TaxIdentity?.Kind.ToString(),
            profile.TaxIdentity?.Masked().Number,
            profile.TaxIdentity?.TaxOffice);

    private sealed record AccountRow(Guid CustomerId, decimal Balance, decimal CreditLimit, int? PaymentTermDays);

    private async Task<IReadOnlyList<AccountRow>> ReadAccountRowsAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT p.customer_id, COALESCE(b.current_balance, 0), COALESCE(t.credit_limit, 0), t.payment_term_days
            FROM customer_data.profiles p
            LEFT JOIN customer_account.balances b ON b.customer_id = p.customer_id
            LEFT JOIN customer_account.credit_terms t ON t.customer_id = p.customer_id
            WHERE NOT p.anonymized AND (@customer_id IS NULL OR p.customer_id = @customer_id)
            ORDER BY p.created_at DESC
            LIMIT @limit;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = (object?)customerId ?? DBNull.Value;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = MaxListed;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<AccountRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AccountRow(
                reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2),
                await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetInt32(3)));
        }

        return rows;
    }
}

/// <summary>A till request the customer account surface refuses with a Turkish message for the cashier.</summary>
public sealed class CustomerAccountValidationException(string turkishMessage) : ArgumentException(turkishMessage)
{
    public string TurkishMessage { get; } = turkishMessage;
}
