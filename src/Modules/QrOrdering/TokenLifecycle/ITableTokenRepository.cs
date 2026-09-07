namespace ALKAROS.QrOrdering.TokenLifecycle;

public interface ITableTokenRepository
{
    /// <summary>Looks up a token by its hash — the only way a raw token is ever resolved.</summary>
    Task<TableToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>The table's current non-revoked token row, if any (may still be time-expired — callers check).</summary>
    Task<TableToken?> GetActiveForTableAsync(Guid tableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new token row. Throws a <see cref="Npgsql.PostgresException"/>
    /// unique-violation on ux_table_tokens_active_per_table if the table
    /// already has a non-revoked token — callers translate that into
    /// <see cref="TableTokenAlreadyActiveException"/>.
    /// </summary>
    Task AddAsync(TableToken token, CancellationToken cancellationToken = default);

    /// <summary>Revokes one token by id. Throws <see cref="TableTokenNotFoundException"/> if it does not exist or is already revoked.</summary>
    Task RevokeAsync(Guid tokenId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically revokes <paramref name="previousTokenId"/> and inserts
    /// <paramref name="newToken"/> in one transaction — a crash between the
    /// two steps must never leave the table with zero or two active tokens.
    /// Throws <see cref="TableTokenNotFoundException"/> if the previous token
    /// does not exist or is already revoked.
    /// </summary>
    Task RotateAsync(Guid previousTokenId, string revokedReason, TableToken newToken, CancellationToken cancellationToken = default);
}
