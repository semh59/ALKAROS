using ALKAROS.Inventory.StockMaster;
using ALKAROS.Purchasing.Suppliers;

namespace ALKAROS.Purchasing.PurchaseInvoices;

public interface IPurchaseInvoiceService
{
    Task<PurchaseInvoice> ImportAsync(string xml, string importedBy, string source, CancellationToken ct = default);

    Task<PurchaseInvoice> GetAsync(Guid invoiceId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseInvoiceSummary>> ListAsync(string? status, CancellationToken ct = default);

    /// <summary>Maps a draft line to a stock item and returns the invoice as it stands afterwards.</summary>
    Task<PurchaseInvoice> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default);
}

public sealed class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly IPurchaseInvoiceRepository _invoices;
    private readonly ISupplierRepository _suppliers;
    private readonly IStockItemRepository _stockItems;

    public PurchaseInvoiceService(IPurchaseInvoiceRepository invoices, ISupplierRepository suppliers, IStockItemRepository stockItems)
    {
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
    }

    public async Task<PurchaseInvoice> ImportAsync(string xml, string importedBy, string source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importedBy);
        var parsed = UblPurchaseInvoiceParser.Parse(xml);

        var supplier = await _suppliers.GetByTaxNumberAsync(parsed.SupplierTaxNumber, ct).ConfigureAwait(false);
        var remembered = (await _invoices.FindMappingsAsync(
            parsed.SupplierTaxNumber, parsed.Lines.Select(l => l.ItemKey).Distinct().ToArray(), ct).ConfigureAwait(false))
            .ToDictionary(m => (m.ItemKey, m.PurchaseUnitCode));

        var lines = parsed.Lines.Select(l =>
        {
            remembered.TryGetValue((l.ItemKey, l.UnitCode), out var mapping);
            return new PurchaseInvoiceLine(
                Guid.NewGuid(), l.LineNumber, l.ItemKey, l.SupplierItemCode, l.Description, l.Quantity, l.UnitCode, l.UnitPrice, l.LineNet,
                mapping?.StockItemId, mapping?.ConversionFactor);
        }).ToArray();

        var invoice = new PurchaseInvoice(
            Guid.NewGuid(), parsed.Ettn, parsed.InvoiceNumber, parsed.IssueDate, parsed.SupplierTaxNumber, parsed.SupplierName,
            supplier?.Id, parsed.Currency, source, PurchaseInvoiceStatuses.Draft, importedBy, DateTimeOffset.UtcNow, lines);
        await _invoices.InsertAsync(invoice, xml, ct).ConfigureAwait(false);
        return invoice;
    }

    public async Task<PurchaseInvoice> GetAsync(Guid invoiceId, CancellationToken ct = default)
        => await _invoices.GetAsync(invoiceId, ct).ConfigureAwait(false) ?? throw new PurchaseInvoiceNotFoundException(invoiceId);

    public Task<IReadOnlyList<PurchaseInvoiceSummary>> ListAsync(string? status, CancellationToken ct = default)
    {
        if (status is not null && status is not (PurchaseInvoiceStatuses.Draft or PurchaseInvoiceStatuses.Approved or PurchaseInvoiceStatuses.Rejected))
            throw new InvalidPurchaseInvoiceException($"Unknown status '{status}'.");
        return _invoices.ListAsync(status, ct);
    }

    public async Task<PurchaseInvoice> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default)
    {
        if (conversionFactor <= 0)
            throw new InvalidPurchaseInvoiceException("The conversion factor must be positive.");
        if (await _stockItems.GetByIdAsync(stockItemId, ct).ConfigureAwait(false) is null)
            throw new InvalidPurchaseInvoiceException($"Stock item '{stockItemId}' was not found.");

        await _invoices.MapLineAsync(invoiceId, lineId, stockItemId, conversionFactor, ct).ConfigureAwait(false);
        return await GetAsync(invoiceId, ct).ConfigureAwait(false);
    }
}
