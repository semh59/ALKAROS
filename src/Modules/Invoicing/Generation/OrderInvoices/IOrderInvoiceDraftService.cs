namespace ALKAROS.Invoicing.Generation.OrderInvoices;

public interface IOrderInvoiceDraftService
{
    /// <summary>
    /// Drafts the order's invoice, or returns the one already drafted (an order has at most one).
    /// Throws <see cref="SellerProfileMissingException"/> when the seller profile is not complete and
    /// <see cref="OrderInvoiceNothingToInvoiceException"/> when the order has no line.
    /// </summary>
    Task<OrderInvoiceDraftResult> CreateAsync(
        OrderInvoiceInput input, Guid? createdBy, CancellationToken cancellationToken = default);

    Task<OrderInvoiceDraft?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
