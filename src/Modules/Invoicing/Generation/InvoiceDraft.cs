namespace ALKAROS.Invoicing.Generation;

/// <summary>
/// The GIB document the invoice is issued as. Which one applies depends on whether the buyer is a registered e-Fatura
/// user; that lookup is QNB's (V14-QNB-001), so the caller passes the choice in.
/// </summary>
public enum InvoiceProfile
{
    EFatura,
    EArsiv,
}

public enum InvoiceStatus
{
    Draft,
}

/// <summary>The buyer as the customer record showed them when the invoice was generated; later edits do not change it.</summary>
public sealed record InvoiceBuyerSnapshot(
    string Name,
    string TaxIdKind,
    string TaxIdNumber,
    string? TaxOffice,
    string? Address,
    string? Email);

/// <summary>
/// One tax group of the invoice: every charged amount at the same KDV rate. Amounts are tax-inclusive
/// (V0-CMP-002), so <see cref="GrossAmount"/> is what the buyer owes for the group and
/// <see cref="NetAmount"/> + <see cref="TaxAmount"/> = <see cref="GrossAmount"/> exactly.
/// </summary>
public sealed record InvoiceDraftLine(
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount);

/// <summary>
/// V14-INV-002: an invoice generated from one source set. It is a draft in the GIB sense - not yet numbered or sent
/// (V14-QNB-002 does both) - but immutable: the database refuses any change to its lines, amounts, buyer or source.
/// <see cref="InvoiceId"/> is the UBL UUID (ETTN).
/// </summary>
public sealed record InvoiceDraft(
    Guid InvoiceId,
    Guid SourceSetId,
    Guid PeriodId,
    Guid CustomerId,
    InvoiceProfile Profile,
    string UblProfileId,
    string InvoiceTypeCode,
    string CurrencyCode,
    DateOnly IssueDate,
    InvoiceStatus Status,
    InvoiceBuyerSnapshot Buyer,
    decimal LineExtensionAmount,
    decimal TaxTotal,
    decimal PayableAmount,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    IReadOnlyList<InvoiceDraftLine> Lines);

/// <summary>The outcome of a generation: the invoice, and whether this call created it or found the existing one.</summary>
public sealed record InvoiceGenerationResult(InvoiceDraft Invoice, bool WasAlreadyGenerated);
