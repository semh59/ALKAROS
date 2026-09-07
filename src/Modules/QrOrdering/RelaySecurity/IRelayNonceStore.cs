namespace ALKAROS.QrOrdering.RelaySecurity;

public interface IRelayNonceStore
{
    /// <summary>
    /// Atomically records (tokenId, nonce) as used. Returns <c>true</c> the
    /// first time this pair is seen; <c>false</c> when it was already
    /// recorded — the caller's request is a replay.
    /// </summary>
    Task<bool> TryConsumeAsync(Guid tokenId, Guid nonce, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
}
