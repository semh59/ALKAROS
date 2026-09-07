using ALKAROS.QrOrdering.TokenLifecycle;

namespace ALKAROS.QrOrdering.CustomerSession;

/// <summary>
/// V12-QRS-003: exchanges a validated table token (V12-QRS-001) for a
/// customer session exactly once, and validates/renews that session
/// afterward — the raw table token is never reused as a session credential.
/// </summary>
public sealed class CustomerSessionService
{
    /// <summary>No activity for this long ends the session even if its absolute lifetime has not elapsed.</summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>Matches <see cref="TableTokenService.DefaultLifetime"/> — a session should not outlive the dining visit its table token was scoped to.</summary>
    public static readonly TimeSpan DefaultAbsoluteLifetime = TimeSpan.FromHours(4);

    private readonly ICustomerSessionRepository _repository;
    private readonly TableTokenService _tableTokenService;
    private readonly TimeSpan _idleTimeout;
    private readonly TimeSpan _absoluteLifetime;

    public CustomerSessionService(
        ICustomerSessionRepository repository,
        TableTokenService tableTokenService,
        TimeSpan? idleTimeout = null,
        TimeSpan? absoluteLifetime = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tableTokenService = tableTokenService ?? throw new ArgumentNullException(nameof(tableTokenService));
        _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
        _absoluteLifetime = absoluteLifetime ?? DefaultAbsoluteLifetime;
        if (_idleTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), idleTimeout, "Idle timeout must be positive.");
        if (_absoluteLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(absoluteLifetime), absoluteLifetime, "Absolute lifetime must be positive.");
    }

    /// <summary>
    /// Validates <paramref name="rawTableToken"/> (V12-QRS-001) exactly once
    /// and, on success, issues a brand-new customer session scoped to that
    /// token's table. Never throws for an invalid table token — an
    /// expired/revoked/unknown QR is a routine outcome, not an error.
    /// </summary>
    public async Task<CustomerSessionIssueResult> IssueAsync(string rawTableToken, CancellationToken cancellationToken = default)
    {
        var tokenValidation = await _tableTokenService.ValidateAsync(rawTableToken, cancellationToken);
        if (!tokenValidation.IsValid)
            return CustomerSessionIssueResult.Invalid(tokenValidation.FailureReason!);

        var (raw, hash) = CustomerSessionTokenGenerator.Create();
        var now = DateTimeOffset.UtcNow;
        var session = new CustomerSession(Guid.NewGuid(), tokenValidation.TableId!.Value, hash, now, now, now + _absoluteLifetime);
        await _repository.AddAsync(session, cancellationToken);

        return CustomerSessionIssueResult.Issued(raw, session.TableId, session.SessionId);
    }

    /// <summary>
    /// Validates a raw session token presented by a caller and, on success,
    /// slides its idle window forward. Never throws for an invalid session —
    /// expired/idle/revoked/unknown are expected, routine outcomes.
    /// </summary>
    public async Task<CustomerSessionValidationResult> ValidateAsync(string rawSessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawSessionToken))
            return CustomerSessionValidationResult.Invalid("NOT_FOUND");

        var hash = CustomerSessionTokenGenerator.Hash(rawSessionToken);
        var session = await _repository.GetByHashAsync(hash, cancellationToken);
        if (session is null)
            return CustomerSessionValidationResult.Invalid("NOT_FOUND");
        if (session.IsRevoked)
            return CustomerSessionValidationResult.Invalid("REVOKED");

        var now = DateTimeOffset.UtcNow;
        if (session.IsAbsolutelyExpired(now))
            return CustomerSessionValidationResult.Invalid("EXPIRED");
        if (session.IsIdleExpired(now, _idleTimeout))
            return CustomerSessionValidationResult.Invalid("IDLE_EXPIRED");

        await _repository.TouchActivityAsync(session.SessionId, now, cancellationToken);
        return CustomerSessionValidationResult.Valid(session.TableId, session.SessionId);
    }

    /// <summary>Explicitly revokes a session (e.g. the customer's own "end session" action, or a staff-triggered table reset).</summary>
    public Task RevokeAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default) =>
        _repository.RevokeAsync(sessionId, DateTimeOffset.UtcNow, reason, cancellationToken);
}

public sealed record CustomerSessionIssueResult(bool IsValid, string? RawToken, Guid? TableId, Guid? SessionId, string? FailureReason)
{
    public static CustomerSessionIssueResult Issued(string rawToken, Guid tableId, Guid sessionId) => new(true, rawToken, tableId, sessionId, null);

    public static CustomerSessionIssueResult Invalid(string reason) => new(false, null, null, null, reason);
}

/// <summary>
/// A captured raw session only ever resolves to the one table it was issued
/// for — a caller must check <see cref="TableId"/> against the table it
/// expects before honoring the session, the same defense-in-depth pattern
/// as `RelayCredentialAccessPolicy`'s accessor check.
/// </summary>
public sealed record CustomerSessionValidationResult(bool IsValid, Guid? TableId, Guid? SessionId, string? FailureReason)
{
    public static CustomerSessionValidationResult Valid(Guid tableId, Guid sessionId) => new(true, tableId, sessionId, null);

    public static CustomerSessionValidationResult Invalid(string reason) => new(false, null, null, reason);
}
