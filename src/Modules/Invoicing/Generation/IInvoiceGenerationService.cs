namespace ALKAROS.Invoicing.Generation;

/// <summary>
/// V14-INV-002: turns one live source set (V14-INV-001) into an immutable invoice draft with KDV-grouped lines and a
/// snapshot of the buyer's tax identity (V1-RMD-453). Only the set's Charge transactions are invoiced - they are the
/// sales; payments and other movements stay in the set for reconciliation. Each charge is split across the KDV rates
/// of the bill it paid. Generation reads the account ledger, billing and payments by SQL and writes only the
/// invoicing schema, so it never adds a second debit to the customer's balance (V0-DOM-007 invariant 1).
/// </summary>
public interface IInvoiceGenerationService
{
    /// <summary>
    /// Generates the invoice of <paramref name="sourceSetId"/>. A retry returns the invoice already generated for the
    /// set; asking for it again as another profile is refused.
    /// </summary>
    Task<InvoiceGenerationResult> GenerateAsync(
        Guid sourceSetId, InvoiceProfile profile, Guid generatedBy, CancellationToken cancellationToken = default);

    Task<InvoiceDraft?> GetAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceDraft?> GetBySourceSetAsync(Guid sourceSetId, CancellationToken cancellationToken = default);
}
