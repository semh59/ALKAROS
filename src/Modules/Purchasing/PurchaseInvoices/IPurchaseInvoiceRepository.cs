using Npgsql;

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
    /// <summary>Moves a draft to a new status inside the caller's transaction; false when it was no longer in the expected status.</summary>
    Task<bool> TryTransitionAsync(Guid invoiceId, string expectedStatus, string newStatus, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);

    /// <summary>Writes the order-less goods receipt an approved invoice stands for, in the caller's transaction.</summary>
    Task InsertReceiptAsync(InvoiceReceipt receipt, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);

    Task<long> GetInboxCursorAsync(string source, CancellationToken ct = default);

    /// <summary>Moves the resume point forward; a lower value never overwrites a higher one.</summary>
    Task SaveInboxCursorAsync(string source, long lastSequence, CancellationToken ct = default);

    Task<int> MapLineAsync(Guid invoiceId, Guid lineId, Guid stockItemId, decimal conversionFactor, CancellationToken ct = default);
}

public sealed record InvoiceReceiptLine(Guid StockItemId, decimal Quantity, string UnitCode, decimal UnitPrice);

public sealed record InvoiceReceipt(
    Guid ReceiptId, string ReceiptNumber, Guid InvoiceId, Guid SupplierId, Guid LocationId, DateTimeOffset ReceivedAt, string ReceivedBy,
    string Notes, IReadOnlyList<InvoiceReceiptLine> Lines);
