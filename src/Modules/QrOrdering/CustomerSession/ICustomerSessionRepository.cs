namespace ALKAROS.QrOrdering.CustomerSession;

public interface ICustomerSessionRepository
{
    /// <summary>Looks up a session by its hash — the only way a raw session token is ever resolved.</summary>
    Task<CustomerSession?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new session row.</summary>
    Task AddAsync(CustomerSession session, CancellationToken cancellationToken = default);

    /// <summary>Slides the idle window forward — called on every successful validation.</summary>
    Task TouchActivityAsync(Guid sessionId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Revokes one session by id. Throws <see cref="CustomerSessionNotFoundException"/> if it does not exist or is already revoked.</summary>
    Task RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken = default);
}
