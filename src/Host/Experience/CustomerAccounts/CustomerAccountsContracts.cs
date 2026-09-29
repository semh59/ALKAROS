namespace ALKAROS.Host.Experience.CustomerAccounts;

/// <summary>A customer as the till sees them: name, a masked phone, what they owe and how much more they may owe.</summary>
public sealed record CustomerAccountSummaryV1(
    Guid CustomerId,
    string Name,
    string? PhoneMasked,
    decimal Balance,
    decimal CreditLimit,
    decimal AvailableCredit,
    int? PaymentTermDays);

public sealed record CreateCustomerV1(string? Name, string? Phone);

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
