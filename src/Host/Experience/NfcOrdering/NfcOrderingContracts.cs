namespace ALKAROS.Host.Experience.NfcOrdering;

/// <summary>
/// V12-NFC-001. <paramref name="Id"/> is the client-generated correlation id
/// for this whole "tap and order" submission — the customer's browser
/// generates it once and resends the identical value on every retry (page
/// reload after a dropped connection, double tap). Persisted as the
/// resulting order's <see cref="ALKAROS.Orders.OrderAggregate.Order.SourceReferenceId"/>
/// and enforced by the same partial unique index
/// (<c>ux_orders_table_submission</c>, V1-RMD-123) the waiter table-draft
/// flow already relies on: a retry replays the existing order instead of
/// starting a second one. No product name/price fields — unlike the
/// waiter-facing <c>OrderItemDraftDto</c>, a customer-facing request is
/// never trusted for pricing; the server always resolves both from
/// <c>catalog.products</c>.
/// </summary>
public sealed record NfcOrderRequest(
    IReadOnlyList<NfcOrderItemRequestDto> Items,
    Guid Id);

public sealed record NfcOrderItemRequestDto(
    Guid Id,
    Guid ProductId,
    int Quantity,
    string? SpecialInstructions = null);
