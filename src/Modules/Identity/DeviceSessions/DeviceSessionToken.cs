using System.Security.Cryptography;
using System.Text;

namespace ALKAROS.Identity.DeviceSessions;

/// <summary>
/// Generates opaque raw tokens and their permanent SHA-256 representations.
/// The raw token is returned to the client exactly once at creation; only
/// <see cref="Hash"/> is ever persisted (V0-ARC-002 allowed hashed session
/// tokens; V1-IAM-003 acceptance: raw tokens are never persisted).
/// </summary>
public static class DeviceSessionToken
{
    private const string StorePrefix = "alkaros-device-session:";

    /// <summary>
    /// Generates a new random raw token and returns its SHA-256 hex hash.
    /// </summary>
    public static (string Raw, string Hash) Create()
    {
        var raw = StorePrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (raw, Hash(raw));
    }

    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawToken);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}