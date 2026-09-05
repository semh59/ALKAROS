using System;

namespace ALKAROS.Purchasing.OrdersAndReceipts;

public abstract class PurchasingException : Exception
{
    protected PurchasingException(string message) : base(message) { }
    protected PurchasingException(string message, Exception inner) : base(message, inner) { }
}

public sealed class PurchaseOrderNotFoundException : PurchasingException
{
    public PurchaseOrderNotFoundException(Guid id) : base($"Purchase order '{id}' was not found.") { }
    public PurchaseOrderNotFoundException(string orderNumber) : base($"Purchase order '{orderNumber}' was not found.") { }
}

public sealed class GoodsReceiptNotFoundException : PurchasingException
{
    public GoodsReceiptNotFoundException(Guid id) : base($"Goods receipt '{id}' was not found.") { }
    public GoodsReceiptNotFoundException(string receiptNumber) : base($"Goods receipt '{receiptNumber}' was not found.") { }
}

public sealed class InvalidPurchaseOrderException : PurchasingException
{
    public InvalidPurchaseOrderException(string message) : base(message) { }
}

public sealed class InvalidGoodsReceiptException : PurchasingException
{
    public InvalidGoodsReceiptException(string message) : base(message) { }
}

public sealed class PurchaseOrderStatusException : PurchasingException
{
    public PurchaseOrderStatusException(string message) : base(message) { }
}

public sealed class VarianceReasonRequiredException : PurchasingException
{
    public VarianceReasonRequiredException(string message) : base(message) { }
}

public sealed class OverReceiptApprovalRequiredException : PurchasingException
{
    public OverReceiptApprovalRequiredException(string message) : base(message) { }
}

public sealed class DuplicateGoodsReceiptException : PurchasingException
{
    public DuplicateGoodsReceiptException(string receiptNumber) : base($"Goods receipt '{receiptNumber}' already exists.") { }
}
