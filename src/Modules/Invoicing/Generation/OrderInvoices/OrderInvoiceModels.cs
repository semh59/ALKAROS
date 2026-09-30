namespace ALKAROS.Invoicing.Generation.OrderInvoices;

/// <summary>One counted order item as the order stored it: tax-inclusive, so net + tax = gross.</summary>
public sealed record OrderInvoiceLineInput(
    string Description,
    decimal Quantity,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount);

/// <summary>
/// What the caller knows about a handed-over platform order. The internet-sale fields (web address, payment
/// method and date, carrier) are the ones GIB asks for on an e-Arsiv invoice for a sale made over the internet.
/// </summary>
public sealed record OrderInvoiceInput(
    Guid OrderId,
    string Provider,
    string ExternalOrderId,
    string OrderNumber,
    DateOnly ServiceDate,
    string WebAddress,
    string? PaymentMethod,
    DateOnly? PaymentDate,
    string? CarrierName,
    string? CarrierTaxId,
    IReadOnlyList<OrderInvoiceLineInput> Lines);

public sealed record OrderInvoiceLine(
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount);

/// <summary>The seller as it stood when the invoice was drafted.</summary>
public sealed record OrderInvoiceSeller(
    string LegalName,
    string TaxIdKind,
    string TaxIdNumber,
    string TaxOffice,
    string Address);

/// <summary>
/// An e-Arsiv invoice for one platform order. A draft in the GIB sense: not numbered or sent, but immutable apart
/// from its status. <see cref="InvoiceId"/> is the UBL UUID (ETTN). The buyer is a final consumer.
/// </summary>
public sealed record OrderInvoiceDraft(
    Guid InvoiceId,
    Guid OrderId,
    string Provider,
    string ExternalOrderId,
    string OrderNumber,
    DateOnly IssueDate,
    DateOnly ServiceDate,
    InvoiceStatus Status,
    OrderInvoiceSeller Seller,
    string WebAddress,
    string? PaymentMethod,
    DateOnly? PaymentDate,
    string? CarrierName,
    string? CarrierTaxId,
    decimal LineExtensionAmount,
    decimal TaxTotal,
    decimal PayableAmount,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    IReadOnlyList<OrderInvoiceLine> Lines);

public sealed record OrderInvoiceDraftResult(OrderInvoiceDraft Invoice, bool WasAlreadyCreated);

/// <summary>The seller profile is not filled in (or incomplete), so no invoice can be drafted.</summary>
public sealed class SellerProfileMissingException()
    : InvalidOperationException("The seller profile is missing or incomplete; fill it in before drafting invoices.");

public sealed class OrderInvoiceNothingToInvoiceException(Guid orderId)
    : InvalidOperationException($"Order {orderId} has no counted line to invoice.");
