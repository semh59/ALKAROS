using System.Security.Cryptography;

namespace ALKAROS.Identity.Authentication;

/// <summary>
/// A stateless secure session token produced on successful login. The token
/// itself is only ever returned to the caller; persistence and revocation of
/// its SHA-256 hash belong to V1-IAM-003 (device_sessions).
/// </summary>
public sealed record IssuedSessionToken(string Token, string TokenHash, DateTimeOffset ExpiresAt);

/// <summary>
/// Produces cryptographically random session tokens with a default lifetime
/// of 12 hours.
/// </summary>
public static class SessionTokenIssuer
{
    public const int TokenBytes = 32;
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(12);

    public static IssuedSessionToken Issue(DateTimeOffset now, TimeSpan? lifetime = null)
    {
        var effectiveLifetime = lifetime ?? DefaultLifetime;
        if (effectiveLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be positive.");

        var tokenBytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));

        return new IssuedSessionToken(token, tokenHash, now.Add(effectiveLifetime));
    }
}
