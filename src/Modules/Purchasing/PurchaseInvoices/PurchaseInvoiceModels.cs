namespace ALKAROS.Purchasing.PurchaseInvoices;

public static class PurchaseInvoiceSources
{
    public const string XmlUpload = "XmlUpload";
    public const string QnbInbox = "QnbInbox";
}

public static class PurchaseInvoiceStatuses
{
    public const string Draft = "Draft";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public sealed record ParsedInvoiceLine(
    int LineNumber, string? SupplierItemCode, string Description, decimal Quantity, string UnitCode, decimal UnitPrice, decimal LineNet)
{
    /// <summary>Stable identity of the supplier's product: its own item code when it prints one, else the normalised name.</summary>
    public string ItemKey => PurchaseInvoiceItemKey.For(SupplierItemCode, Description);
}

public sealed record ParsedPurchaseInvoice(
    Guid Ettn, string InvoiceNumber, DateOnly IssueDate, string SupplierTaxNumber, string SupplierName, string Currency,
    IReadOnlyList<ParsedInvoiceLine> Lines);

public sealed record PurchaseInvoiceLine(
    Guid LineId, int LineNumber, string ItemKey, string? SupplierItemCode, string Description, decimal Quantity, string UnitCode,
    decimal UnitPrice, decimal LineNet, Guid? StockItemId, decimal? ConversionFactor);

public sealed record PurchaseInvoice(
    Guid InvoiceId, Guid Ettn, string InvoiceNumber, DateOnly IssueDate, string SupplierTaxNumber, string SupplierName,
    Guid? SupplierId, string Currency, string Source, string Status, string ImportedBy, DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseInvoiceLine> Lines);

public sealed record PurchaseInvoiceSummary(
    Guid InvoiceId, string InvoiceNumber, DateOnly IssueDate, string SupplierName, Guid? SupplierId, string Status,
    int LineCount, int UnmappedLineCount, decimal NetTotal);

public sealed record SupplierItemMapping(string SupplierTaxNumber, string ItemKey, string PurchaseUnitCode, Guid StockItemId, decimal ConversionFactor);
