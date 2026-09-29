namespace ALKAROS.Host.Experience.CustomerAccounts;

/// <summary>
/// A customer as the till sees them: name, a masked phone, what they owe and how much more they may owe, and the
/// invoice tax identity (V1-RMD-453) with its number masked - TaxIdKind is "Vkn" or "Tckn".
/// </summary>
public sealed record CustomerAccountSummaryV1(
    Guid CustomerId,
    string Name,
    string? PhoneMasked,
    decimal Balance,
    decimal CreditLimit,
    decimal AvailableCredit,
    int? PaymentTermDays,
    string? TaxIdKind = null,
    string? TaxIdMasked = null,
    string? TaxOffice = null);

/// <summary>The tax identity is optional; when given, TaxIdKind is "Vkn" or "Tckn".</summary>
public sealed record CreateCustomerV1(
    string? Name, string? Phone, string? TaxIdKind = null, string? TaxIdNumber = null, string? TaxOffice = null);

/// <summary>V1-RMD-453: replaces a customer's tax identity; all three empty removes it.</summary>
public sealed record UpdateCustomerTaxIdentityV1(string? TaxIdKind, string? TaxIdNumber, string? TaxOffice);

public sealed record CustomerStatementEntryV1(
    Guid Id, DateTimeOffset OccurredAt, string Type, string Description, decimal Amount, decimal SignedAmount);

public sealed record CustomerReceiptV1(string ReceiptNumber, Guid AccountPaymentId, decimal Amount, DateTimeOffset IssuedAt);

public sealed record CustomerStatementV1(
    CustomerAccountSummaryV1 Customer,
    IReadOnlyList<CustomerStatementEntryV1> Entries,
    IReadOnlyList<CustomerReceiptV1> Receipts);

public sealed record AccountChargeRequestV1(Guid CustomerId, decimal Amount, string IdempotencyKey);

public sealed record AccountChargeResultV1(decimal ApprovedAmount, bool BillClosed, bool WasReplayed, decimal BalanceAfter);

public sealed record AccountReceiptRequestV1(Guid CustomerId, decimal Amount, string IdempotencyKey);

public sealed record AccountReceiptResultV1(
    Guid AccountPaymentId, string ReceiptNumber, decimal Amount, decimal BalanceAfter, bool WasReplayed);
