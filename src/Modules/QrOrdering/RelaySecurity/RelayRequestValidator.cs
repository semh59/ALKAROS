using ALKAROS.QrOrdering.TokenLifecycle;

namespace ALKAROS.QrOrdering.RelaySecurity;

public sealed record RelayRequestValidationResult(bool IsValid, Guid? TableId, string? FailureReason)
{
    public static RelayRequestValidationResult Valid(Guid tableId) => new(true, tableId, null);

    public static RelayRequestValidationResult Invalid(string reason) => new(false, null, reason);
}

/// <summary>
/// V12-QRS-002: authenticates a request arriving through the public relay
/// before it ever reaches order business logic. Layers on top of
/// `V12-QRS-001`'s table token — the token itself is the "key"; this adds
/// the two properties a bare token check does not give you: a captured and
/// resent request must fail (replay), and a request timestamped far from
/// now must fail (bounds how long a captured request stays exploitable and
/// how long the nonce store needs to retain rows).
/// </summary>
public sealed class RelayRequestValidator
{
    /// <summary>How far a request's own timestamp may drift from server time. 2 minutes: generous for clock skew and network latency, tight enough that a captured request has a short exploit window.</summary>
    public static readonly TimeSpan DefaultTimestampWindow = TimeSpan.FromMinutes(2);

    private readonly TableTokenService _tokens;
    private readonly IRelayNonceStore _nonces;
    private readonly TimeSpan _timestampWindow;

    public RelayRequestValidator(TableTokenService tokens, IRelayNonceStore nonces, TimeSpan? timestampWindow = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _nonces = nonces ?? throw new ArgumentNullException(nameof(nonces));
        _timestampWindow = timestampWindow ?? DefaultTimestampWindow;
        if (_timestampWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timestampWindow), timestampWindow, "Timestamp window must be positive.");
    }

    /// <summary>
    /// Order matters and is deliberate: an invalid/expired/revoked token is
    /// rejected before the timestamp or nonce is even looked at (cheapest
    /// check first, and a request with no valid token has no legitimate
    /// nonce space to consume from). Never throws for a bad request — a
    /// rejected relay request is a routine outcome, not a server error.
    /// </summary>
    public async Task<RelayRequestValidationResult> ValidateAsync(
        string rawTableToken,
        Guid requestNonce,
        DateTimeOffset requestTimestamp,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await _tokens.ValidateAsync(rawTableToken, cancellationToken);
        if (!tokenResult.IsValid)
            return RelayRequestValidationResult.Invalid(tokenResult.FailureReason!);

        var now = DateTimeOffset.UtcNow;
        if ((now - requestTimestamp).Duration() > _timestampWindow)
            return RelayRequestValidationResult.Invalid("TIMESTAMP_OUT_OF_WINDOW");

        var isFresh = await _nonces.TryConsumeAsync(tokenResult.TokenId!.Value, requestNonce, now, cancellationToken);
        if (!isFresh)
            return RelayRequestValidationResult.Invalid("REPLAYED");

        return RelayRequestValidationResult.Valid(tokenResult.TableId!.Value);
    }
}
