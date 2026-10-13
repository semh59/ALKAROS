using ALKAROS.Purchasing.OrdersAndReceipts;

namespace ALKAROS.Purchasing.PurchaseInvoices;

public sealed class InvalidPurchaseInvoiceException : PurchasingException
{
    public InvalidPurchaseInvoiceException(string message) : base(message) { }
    public InvalidPurchaseInvoiceException(string message, Exception inner) : base(message, inner) { }
}

public sealed class UnsupportedPurchaseDocumentException : PurchasingException
{
    public UnsupportedPurchaseDocumentException(string message) : base(message) { }
}

/// <summary>The source answered but would not hand this one document over (a session problem looks the same as a bad document).</summary>
public sealed class PurchaseInvoiceDocumentRefusedException : PurchasingException
{
    public PurchaseInvoiceDocumentRefusedException(string message, Exception inner) : base(message, inner) { }
}

public sealed class DuplicatePurchaseInvoiceException : PurchasingException
{
    public DuplicatePurchaseInvoiceException(Guid ettn) : base($"A purchase invoice with ETTN '{ettn}' was already imported.") { }
}

public sealed class PurchaseInvoiceNotFoundException : PurchasingException
{
    public PurchaseInvoiceNotFoundException(Guid id) : base($"Purchase invoice '{id}' was not found.") { }
}

public sealed class PurchaseInvoiceNotReadyException : PurchasingException
{
    public PurchaseInvoiceNotReadyException(string message) : base(message) { }
}

public sealed class PurchaseInvoiceStatusException : PurchasingException
{
    public PurchaseInvoiceStatusException(string message) : base(message) { }
}
