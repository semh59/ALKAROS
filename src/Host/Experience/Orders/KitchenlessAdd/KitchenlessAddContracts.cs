namespace ALKAROS.Host.Experience.Orders.KitchenlessAdd;

/// <param name="ReasonCode">One of <see cref="KitchenlessAddStore.ReasonCodes"/>.</param>
public sealed record KitchenlessAddRequestV1(
    string IdempotencyKey, long ExpectedRowVersion, Guid ProductId, decimal Quantity, string? ReasonCode);

public sealed record KitchenlessAddResultV1(Guid OrderId, Guid ItemId, long RowVersion, string ReasonCode);

public sealed class KitchenlessAddReasonException() : Exception("A known reason code is required.");

public sealed class KitchenlessAddQuantityException() : Exception("Quantity must be positive and at most 999.");

public sealed class KitchenlessAddKeyException() : Exception("An idempotency key is required.");

public sealed class KitchenlessAddProductException(Guid productId)
    : Exception($"Product {productId} was not found or has no active price.");

/// <summary>The check is no longer open (paid, cancelled) or already has a bill, so a line added now would miss the bill.</summary>
public sealed class KitchenlessAddNotAllowedException(Guid orderId, string status)
    : Exception($"Order {orderId} in state {status} cannot take a kitchen-less line.");

public static class KitchenlessAddMessages
{
    public const string Reason = "Ekleme nedeni seçilmeli: garson unuttu, müşteri istedi veya diğer.";
    public const string Quantity = "Adet 0'dan büyük, en çok 999 olmalı.";
    public const string Key = "İşlem anahtarı eksik; ekranı yenileyip tekrar deneyin.";
    public const string Product = "Ürün bulunamadı veya fiyatı yok.";
    public const string NotAllowed = "Bu hesaba şu an ürün eklenemez; hesap açılmış veya kapanmış olabilir.";
}
