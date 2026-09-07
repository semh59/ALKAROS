namespace ALKAROS.QrOrdering.CustomerSession;

/// <summary>
/// A hashed, revocable browser credential issued in exchange for a validated
/// table token (qr_ordering.customer_sessions, V12-QRS-003) — the raw table
/// token itself is never reused as a session credential; this is a separate
/// value the raw table token is exchanged for exactly once at issuance.
/// Several customers at the same table each hold their own session (unlike
/// the one-per-table table token). Two independent expiry clocks apply: an
/// idle window (no activity for that long) and an absolute lifetime (the
/// session is retired regardless of activity) — both are policy the caller
/// supplies at check time via <see cref="IsActive"/>, not stored on the row.
/// </summary>
public sealed class CustomerSession
{
    public CustomerSession(
        Guid sessionId,
        Guid tableId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset lastActivityAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt = null,
        string? revokedReason = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session id cannot be empty.", nameof(sessionId));
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty.", nameof(tokenHash));
        if (expiresAt <= issuedAt)
            throw new ArgumentException("Expiry must be after issuance.", nameof(expiresAt));
        if (lastActivityAt < issuedAt)
            throw new ArgumentException("Last activity cannot precede issuance.", nameof(lastActivityAt));
        if (revokedAt is null != revokedReason is null)
            throw new ArgumentException("RevokedAt and RevokedReason must be set together.");

        SessionId = sessionId;
        TableId = tableId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        LastActivityAt = lastActivityAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        RevokedReason = revokedReason;
    }

    public Guid SessionId { get; }
    public Guid TableId { get; }
    public string TokenHash { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset LastActivityAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? RevokedAt { get; }
    public string? RevokedReason { get; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsAbsolutelyExpired(DateTimeOffset asOf) => asOf >= ExpiresAt;

    public bool IsIdleExpired(DateTimeOffset asOf, TimeSpan idleTimeout) => asOf >= LastActivityAt + idleTimeout;

    public bool IsActive(DateTimeOffset asOf, TimeSpan idleTimeout) =>
        !IsRevoked && !IsAbsolutelyExpired(asOf) && !IsIdleExpired(asOf, idleTimeout);

    /// <summary>Returns a revoked copy. Throws if already revoked — callers check first.</summary>
    public CustomerSession Revoke(DateTimeOffset at, string reason)
    {
        if (IsRevoked)
            throw new InvalidOperationException($"Session {SessionId} is already revoked.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason cannot be empty.", nameof(reason));

        return new CustomerSession(SessionId, TableId, TokenHash, IssuedAt, LastActivityAt, ExpiresAt, at, reason);
    }

    /// <summary>Returns a copy with activity extended to <paramref name="at"/> — the sliding half of the idle window.</summary>
    public CustomerSession WithActivityAt(DateTimeOffset at)
    {
        if (at < LastActivityAt)
            throw new ArgumentException("Activity time cannot move backward.", nameof(at));

        return new CustomerSession(SessionId, TableId, TokenHash, IssuedAt, at, ExpiresAt, RevokedAt, RevokedReason);
    }
}

/// <summary>V12-QRS-003: Revoke was called for a session that does not exist.</summary>
public sealed class CustomerSessionNotFoundException : Exception
{
    public CustomerSessionNotFoundException(Guid sessionId) : base($"Customer session {sessionId} was not found.") { }
}
