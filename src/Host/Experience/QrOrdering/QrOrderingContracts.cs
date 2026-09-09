namespace ALKAROS.Host.Experience.QrOrdering;

/// <summary>
/// V12-CWB-001. Exchanges a scanned table's raw QR token for a customer
/// session (V12-QRS-003) exactly once. <paramref name="Nonce"/> and
/// <paramref name="Timestamp"/> are the anti-replay pair
/// <c>RelayRequestValidator</c> (V12-QRS-002) checks before the token itself
/// is even looked at again here — the customer's browser generates a fresh
/// nonce for this one call; it is never reused.
/// </summary>
public sealed record QrSessionIssueRequest(string TableToken, Guid Nonce, DateTimeOffset Timestamp);

/// <summary>
/// <paramref name="SessionToken"/> is the only credential the customer's
/// browser needs from here on — the raw table token is never resent.
/// </summary>
public sealed record QrSessionIssueResponse(string SessionToken, Guid TableId);

/// <summary>
/// V12-CWB-002. The observable outcome of queuing a submission — the real
/// Order is materialized asynchronously by Order's own
/// <c>QrOrderSubmittedConsumer</c> (V0-ARC-001 row 19), so this is
/// deliberately not the final order state; poll <c>/orders/{submissionId}</c>
/// for that.
/// </summary>
public sealed record QrOrderSubmissionResponse(Guid SubmissionId, Guid TableId, DateTimeOffset SubmittedAt);

/// <summary>
/// V12-CWB-002. <paramref name="Status"/> is <c>"Pending"</c> while the
/// outbox delivery to Order's consumer has not landed yet;
/// <paramref name="OrderId"/> is null in that state. Once materialized,
/// <paramref name="Status"/> mirrors the real <c>Order.Status</c> (starts at
/// <c>PendingConfirmation</c> — QR orders always require staff confirmation,
/// docs/design/modules/qr-nfc-ordering.md).
/// </summary>
public sealed record QrOrderPollResponse(Guid SubmissionId, string Status, Guid? OrderId);
