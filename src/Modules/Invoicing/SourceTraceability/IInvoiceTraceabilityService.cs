namespace ALKAROS.Invoicing.SourceTraceability;

/// <summary>One ledger transaction and the gross amount it contributed to an invoice line.</summary>
public sealed record InvoiceLineSource(Guid TransactionId, decimal AllocatedAmount);

public sealed record InvoiceLineTrace(int LineNumber, IReadOnlyList<InvoiceLineSource> Sources);

public sealed record InvoiceTrace(Guid InvoiceId, Guid SourceSetId, IReadOnlyList<InvoiceLineTrace> Lines);

public sealed record InvoiceTraceResult(InvoiceTrace Trace, bool WasAlreadyRecorded);

/// <summary>
/// Links every line of a generated invoice to the Charge transactions of its source set that it was built from. A
/// charge is split across the KDV rates of the bill it paid, so a line draws on several charges and a charge feeds
/// several lines; each link carries the gross amount contributed. Payments and other movements in the set are not
/// invoiced and are not linked.
/// </summary>
public interface IInvoiceTraceabilityService
{
    /// <summary>
    /// Records the links of <paramref name="invoiceId"/>, all or none. A retry returns the links already recorded.
    /// </summary>
    Task<InvoiceTraceResult> RecordAsync(
        Guid invoiceId, Guid recordedBy, CancellationToken cancellationToken = default);

    /// <summary>The recorded links, or null while the invoice has none.</summary>
    Task<InvoiceTrace?> GetAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
