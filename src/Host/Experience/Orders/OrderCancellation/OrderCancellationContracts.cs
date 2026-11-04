namespace ALKAROS.Host.Experience.Orders.OrderCancellation;

public sealed record CancelOrderRequestV1(string IdempotencyKey, long ExpectedRowVersion, string? Reason);

/// <param name="StockRestoredItems">Lines whose stock went back because the kitchen had not started them.</param>
/// <param name="WasteItems">Lines the kitchen had started: waste, no stock returned.</param>
public sealed record CancelOrderResultV1(
    Guid OrderId,
    string Status,
    string Reason,
    DateTimeOffset CancelledAt,
    int ItemsCancelled,
    int StockRestoredItems,
    int WasteItems,
    int BillsCancelled);

public sealed class OrderCancelReasonException() : Exception("A cancellation reason of 1 to 500 characters is required.");

public sealed class OrderCancelNotAllowedException(Guid orderId, string status)
    : Exception($"Order {orderId} in state {status} cannot be cancelled.");

/// <summary>Money has moved on one of the order's bills; the payment must be refunded first.</summary>
public sealed class OrderCancelHasPaymentException(Guid orderId)
    : Exception($"Order {orderId} has payments; refund them before cancelling.");

/// <summary>A line was already served; it cannot be taken back, the check is settled instead.</summary>
public sealed class OrderCancelHasServedItemsException(Guid orderId)
    : Exception($"Order {orderId} has served items and cannot be cancelled.");

public static class OrderCancellationMessages
{
    public const string Reason = "İptal gerekçesi 1 ile 500 karakter arasında olmalı.";
    public const string NotAllowed = "Sipariş bu durumda iptal edilemez.";
    public const string HasPayment = "Bu siparişin hesabında ödeme var; iptal etmeden önce ödeme iade edilmeli.";
    public const string HasServedItems = "Servis edilmiş ürün var; sipariş iptal edilemez, hesabı kapatın.";
}
