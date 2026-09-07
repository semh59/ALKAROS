using Npgsql;

namespace ALKAROS.QrOrdering.TokenLifecycle;

/// <summary>
/// V14-QRS-001: issuance, rotation, validation and revocation of a table's
/// QR token. A table has at most one non-revoked token at a time (enforced
/// by `ux_table_tokens_active_per_table`, a hard cutover — this
/// implementation deliberately has no overlapping-validity grace period:
/// a rotated token stops working the instant the new one is issued,
/// atomically, since the DB does not allow two non-revoked rows for the
/// same table to coexist even transiently).
/// </summary>
public sealed class TableTokenService
{
    /// <summary>docs/architecture/qr-relay-topology.md rule 2: QR tokens expire after 4 hours.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(4);

    private readonly ITableTokenRepository _repository;
    private readonly TimeSpan _lifetime;

    public TableTokenService(ITableTokenRepository repository, TimeSpan? lifetime = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _lifetime = lifetime ?? DefaultLifetime;
        if (_lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Lifetime must be positive.");
    }

    /// <summary>Issues the first token for a table. Throws <see cref="TableTokenAlreadyActiveException"/> if one already exists — use <see cref="RotateAsync"/> to replace it.</summary>
    public async Task<string> IssueAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));

        var existing = await _repository.GetActiveForTableAsync(tableId, cancellationToken);
        if (existing is not null)
            throw new TableTokenAlreadyActiveException(tableId);

        var (raw, hash) = TableTokenGenerator.Create();
        var now = DateTimeOffset.UtcNow;
        var token = new TableToken(Guid.NewGuid(), tableId, hash, now, now + _lifetime);

        try
        {
            await _repository.AddAsync(token, cancellationToken);
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_table_tokens_active_per_table")
        {
            // A concurrent Issue for the same table won the race.
            throw new TableTokenAlreadyActiveException(tableId);
        }

        return raw;
    }

    /// <summary>Replaces a table's current token with a new one, atomically invalidating the old one. Throws <see cref="TableTokenNotFoundException"/> if the table has no active token — use <see cref="IssueAsync"/> for the first one.</summary>
    public async Task<string> RotateAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));

        var existing = await _repository.GetActiveForTableAsync(tableId, cancellationToken)
            ?? throw new TableTokenNotFoundException($"Table {tableId} has no active token to rotate.");

        var (raw, hash) = TableTokenGenerator.Create();
        var now = DateTimeOffset.UtcNow;
        var newToken = new TableToken(Guid.NewGuid(), tableId, hash, now, now + _lifetime);

        await _repository.RotateAsync(existing.TokenId, "rotated", newToken, cancellationToken);
        return raw;
    }

    /// <summary>Explicitly revokes a table's current token (e.g. a manual "invalidate this QR" staff action) without issuing a replacement.</summary>
    public async Task RevokeAsync(Guid tableId, string reason, CancellationToken cancellationToken = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table id cannot be empty.", nameof(tableId));

        var existing = await _repository.GetActiveForTableAsync(tableId, cancellationToken)
            ?? throw new TableTokenNotFoundException($"Table {tableId} has no active token to revoke.");

        await _repository.RevokeAsync(existing.TokenId, DateTimeOffset.UtcNow, reason, cancellationToken);
    }

    /// <summary>
    /// Validates a raw token presented by a caller. Never throws for an
    /// invalid token — an expired/revoked/unknown token is an expected,
    /// routine outcome (someone scanned an old QR), not an error.
    /// </summary>
    public async Task<TableTokenValidationResult> ValidateAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return TableTokenValidationResult.Invalid("NOT_FOUND");

        var hash = TableTokenGenerator.Hash(rawToken);
        var token = await _repository.GetByHashAsync(hash, cancellationToken);
        if (token is null)
            return TableTokenValidationResult.Invalid("NOT_FOUND");
        if (token.IsRevoked)
            return TableTokenValidationResult.Invalid("REVOKED");
        if (token.IsExpired(DateTimeOffset.UtcNow))
            return TableTokenValidationResult.Invalid("EXPIRED");

        return TableTokenValidationResult.Valid(token.TableId, token.TokenId);
    }
}

public sealed record TableTokenValidationResult(bool IsValid, Guid? TableId, Guid? TokenId, string? FailureReason)
{
    public static TableTokenValidationResult Valid(Guid tableId, Guid tokenId) => new(true, tableId, tokenId, null);

    public static TableTokenValidationResult Invalid(string reason) => new(false, null, null, reason);
}
