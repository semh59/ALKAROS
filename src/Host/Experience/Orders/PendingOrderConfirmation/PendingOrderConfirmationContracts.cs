namespace ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;

/// <summary>
/// V1-RMD-137: found by an independent audit (2026-09-09) — no HTTP action
/// anywhere could ever move an order out of PendingConfirmation (the state
/// the trusted-channel age-restriction check, V12-NFC-002, deliberately
/// parks an order in). These two requests are the minimal, channel-agnostic
/// slice of what V12-QRO-003 eventually plans in full (which also covers
/// QR's own portion-reservation-on-accept, out of scope here since QR has
/// no HTTP surface to reach a PendingConfirmation order through yet).
/// </summary>
public sealed record AcceptPendingOrderRequestV1(long ExpectedRowVersion, string? Notes);

public sealed record RejectPendingOrderRequestV1(long ExpectedRowVersion, string Reason);

public sealed record PendingOrderConfirmationResultV1(
    Guid OrderId,
    string NewStatus,
    long NewOrderRowVersion,
    decimal NewOrderTotal,
    int KitchenTicketItemsCancelled,
    bool TableReleased,
    DateTimeOffset AppliedAt);
