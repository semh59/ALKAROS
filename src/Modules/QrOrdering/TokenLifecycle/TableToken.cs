namespace ALKAROS.QrOrdering.TokenLifecycle;

/// <summary>
/// A hashed, time/policy-bound credential bound to exactly one table
/// (qr_ordering.table_tokens, V14-QRS-001). The raw token is never
/// persisted or reconstructable from this record — only its hash. A
/// database leak of this table exposes no usable raw token
/// (docs/architecture/qr-relay-topology.md rule 2: tokens expire after
/// 4 hours; see <see cref="TableTokenService.DefaultLifetime"/>).
/// </summary>
public sealed class TableToken
{
    public TableToken(
        Guid tokenId,
        Guid tableId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt = null,
        string? revokedReason = null)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token id cannot be empty.", nameof(tokenId));
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty.", nameof(tokenHash));
        if (expiresAt <= issuedAt)
            throw new ArgumentException("Expiry must be after issuance.", nameof(expiresAt));
        if (revokedAt is null != revokedReason is null)
            throw new ArgumentException("RevokedAt and RevokedReason must be set together.");

        TokenId = tokenId;
        TableId = tableId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        RevokedReason = revokedReason;
    }

    public Guid TokenId { get; }
    public Guid TableId { get; }
    public string TokenHash { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? RevokedAt { get; }
    public string? RevokedReason { get; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset asOf) => asOf >= ExpiresAt;

    public bool IsActive(DateTimeOffset asOf) => !IsRevoked && !IsExpired(asOf);

    /// <summary>Returns a revoked copy. Throws if already revoked — callers check first.</summary>
    public TableToken Revoke(DateTimeOffset at, string reason)
    {
        if (IsRevoked)
            throw new InvalidOperationException($"Token {TokenId} is already revoked.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason cannot be empty.", nameof(reason));

        return new TableToken(TokenId, TableId, TokenHash, IssuedAt, ExpiresAt, at, reason);
    }
}

/// <summary>V14-QRS-001: Issue was called for a table that already has an active token.</summary>
public sealed class TableTokenAlreadyActiveException : Exception
{
    public TableTokenAlreadyActiveException(Guid tableId)
        : base($"Table {tableId} already has an active token; use Rotate instead of Issue.") { }
}

/// <summary>V14-QRS-001: Rotate/Revoke was called for a table/token that has no active token.</summary>
public sealed class TableTokenNotFoundException : Exception
{
    public TableTokenNotFoundException(string message) : base(message) { }
}
