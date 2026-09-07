namespace ALKAROS.QrOrdering.PendingOrders;

/// <summary>
/// V12-QRO-001. <paramref name="SubmissionId"/> is the client-generated
/// correlation id for this whole submission — the customer's browser
/// generates it once and resends the identical value on every retry (page
/// reload after a dropped connection, double tap). No product name/price
/// fields — like <c>NfcOrderRequest</c>, a customer-facing request is never
/// trusted for pricing; the server always resolves both from
/// <c>catalog.products</c> at submission time.
/// </summary>
public sealed record QrOrderSubmissionRequest(
    IReadOnlyList<QrOrderSubmissionItemRequest> Items,
    Guid SubmissionId);

public sealed record QrOrderSubmissionItemRequest(
    Guid Id,
    Guid ProductId,
    int Quantity,
    string? SpecialInstructions = null);

/// <summary>
/// The observable outcome of <see cref="QrPendingOrderStore.SubmitAsync"/>.
/// <paramref name="SubmittedAt"/> is the pending submission's own recorded
/// timestamp — the "beklemedeki son kullanma tarihi meta verileri" scope
/// item is this metadata, not a second, unenforced expiry: the table itself
/// carries no time-based expiry once it is Reserved
/// (docs/domain/table-reservation-policy.md), so no such field is invented
/// here. The resulting Order is materialized asynchronously by Order's own
/// <c>QrOrderSubmittedConsumer</c> (V0-ARC-001 row 19); poll
/// <see cref="QrPendingOrderStore.FindResultingOrderAsync"/> for it.
/// </summary>
public sealed record QrOrderSubmissionResult(
    Guid SubmissionId,
    Guid TableId,
    DateTimeOffset SubmittedAt);

/// <summary>The Order materialized (or not yet materialized) for a given submission.</summary>
public sealed record QrOrderSubmissionOutcome(
    Guid OrderId,
    string Status);
