namespace ALKAROS.Invoicing.SourceTraceability;

public sealed class InvoiceTraceInvoiceNotFoundException(Guid invoiceId)
    : InvalidOperationException($"Invoice {invoiceId} was not found.");

public sealed class InvoiceTraceMismatchException(Guid invoiceId, string detail)
    : InvalidOperationException($"Invoice {invoiceId} cannot be traced to its charges: {detail}.")
{
    public Guid InvoiceId { get; } = invoiceId;
}
