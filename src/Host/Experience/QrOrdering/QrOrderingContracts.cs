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
