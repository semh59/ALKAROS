using System.Text.Json.Serialization;

namespace ALKAROS.Payments.Token.Draft;

/// <summary>
/// V13-GOV-006 DRAFT — every shape below was copied from real saved example
/// requests/responses in Token's own published "TokenX Documentation" Postman
/// collection (`evidence/v0/integrations/V0-HUG-001/
/// tokenx-documentation.postman_collection.json`), not invented. None of it
/// has been exercised against a real terminal — see README.md "Verified vs
/// NOT verified".
/// </summary>
public sealed record TokenAuthResult(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("result")] TokenAuthResultPayload? Result);

public sealed record TokenAuthResultPayload(
    [property: JsonPropertyName("accessToken")] string AccessToken);

/// <summary>
/// Item line for a basket (`Add Instant Basket` request body). Field names
/// and casing match the Postman collection's "Add Instant Basket" example
/// exactly.
/// </summary>
public sealed record TokenBasketItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("price")] long Price,
    [property: JsonPropertyName("sectionNo")] int SectionNo,
    [property: JsonPropertyName("taxPercent")] int TaxPercent,
    [property: JsonPropertyName("quantity")] int Quantity);

/// <summary>
/// A forced payment routing line — sent to route the payment to a specific
/// card/meal-card app rather than leaving the terminal's own selection menu
/// open (`docs/x-platform/baslangic/banka-yemek-uygulamalari`). `type: 3` is
/// credit card (V13-HUG-001's own scope); `operatorId: 0` or omitted means
/// "any bank / no forced routing" per the same doc's "App Temp (Test
/// Uygulaması)" = 0 convention.
/// </summary>
public sealed record TokenPaymentRoutingItem(
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("operatorId")] int OperatorId)
{
    public const int PaymentTypeCreditCard = 3;
    public const int PaymentTypeMealCard = 7;
}

public sealed record TokenAddInstantBasketRequest(
    [property: JsonPropertyName("basketID")] Guid BasketId,
    [property: JsonPropertyName("checkNumber")] int CheckNumber,
    [property: JsonPropertyName("items")] IReadOnlyList<TokenBasketItem> Items,
    [property: JsonPropertyName("paymentItems")] IReadOnlyList<TokenPaymentRoutingItem>? PaymentItems = null);

public sealed record TokenApiEnvelope<TResult>(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("result")] TResult? Result);

/// <summary>
/// `Get Basket Details` response `result` shape for a COMPLETED basket
/// (the "200 - OK (Completed Basket)" saved example) — this is the shape
/// `TokenBasketClient.PollUntilSettledAsync` waits for. An open/pending
/// basket's `Sale` is null.
/// </summary>
public sealed record TokenBasketDetails(
    [property: JsonPropertyName("basketID")] string BasketId,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("isLocked")] bool IsLocked,
    [property: JsonPropertyName("total")] long? Total,
    [property: JsonPropertyName("sale")] TokenSaleResult? Sale);

/// <summary>
/// The `sale` object's own `status` is the one documented with a fixed
/// 3-value enum in the FAQ webhook table AND in the Wire developer doc's
/// "Satış Durumu JSON'ı" table: 0 = Başarılı, -1 = Başarısız, 99 = Fiş
/// iptal. This is what `TokenTenderHandler` reads to decide Approved vs
/// Declined.
/// </summary>
public sealed record TokenSaleResult(
    [property: JsonPropertyName("basketID")] string BasketId,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("receiptNo")] int? ReceiptNo,
    [property: JsonPropertyName("paymentItems")] IReadOnlyList<TokenSalePaymentItem>? PaymentItems)
{
    public const int SaleStatusSuccessful = 0;
    public const int SaleStatusFailed = -1;
    public const int SaleStatusVoided = 99;
}

public sealed record TokenSalePaymentItem(
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("operatorId")] int OperatorId,
    [property: JsonPropertyName("status")] int Status);
