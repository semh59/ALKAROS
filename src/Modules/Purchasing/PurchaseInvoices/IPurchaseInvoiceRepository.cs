namespace ALKAROS.Purchasing.PurchaseInvoices;

public interface IPurchaseInvoiceRepository
{
    /// <summary>Stores the invoice with its lines; a second import of the same ETTN throws <see cref="DuplicatePurchaseInvoiceException"/>.</summary>
    Task InsertAsync(PurchaseInvoice invoice, string rawXml, CancellationToken ct = default);

    Task<PurchaseInvoice?> GetAsync(Guid invoiceId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseInvoiceSummary>> ListAsync(string? status, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierItemMapping>> FindMappingsAsync(string supplierTaxNumber, IReadOnlyCollection<string> itemKeys, CancellationToken ct = default);

    /// <summary>
    /// Maps one draft line, remembers the mapping for the supplier's item and unit, and applies it to every other draft line
    /// of the same supplier item and unit. Returns the number of lines mapped.
    /// </summary>
    Task<int> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default);
}
