namespace ALKAROS.Invoicing.SourceSelection;

/// <summary>
/// V14-INV-001: a closed invoice period. Dates are Europe/Istanbul calendar days; <see cref="PeriodEnd"/> is exclusive.
/// A period exists only once an operator has closed it, and it is never reopened.
/// </summary>
public sealed record InvoicePeriod(
    Guid PeriodId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTimeOffset ClosedAt,
    Guid ClosedBy);

public enum InvoiceSourceSetStatus
{
    Selected,
    Cancelled,
}

/// <summary>
/// One customer's account transactions locked for invoicing in one closed period. The debit and credit totals are
/// magnitudes (V0-DOM-007: the direction, not the sign, carries the balance effect); <see cref="NetAmount"/> is the
/// period's invoice balance.
/// </summary>
public sealed record InvoiceSourceSet(
    Guid SourceSetId,
    Guid PeriodId,
    Guid CustomerId,
    InvoiceSourceSetStatus Status,
    decimal DebitTotal,
    decimal CreditTotal,
    DateTimeOffset SelectedAt,
    IReadOnlyList<InvoiceSourceLine> Lines)
{
    public decimal NetAmount => DebitTotal - CreditTotal;
}

public sealed record InvoiceSourceLine(
    Guid TransactionId,
    string TransactionType,
    string Direction,
    decimal Amount,
    DateTimeOffset OccurredAt);

/// <summary>The outcome of a selection: the live set, and whether this call created it or found it.</summary>
public sealed record InvoiceSourceSelectionResult(InvoiceSourceSet SourceSet, bool WasAlreadySelected);
