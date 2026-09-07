using System.Security.Cryptography;
using System.Text;

namespace ALKAROS.QrOrdering.TokenLifecycle;

/// <summary>
/// Generates opaque raw table tokens and their permanent SHA-256
/// representations. The raw token is returned to the caller exactly once at
/// issuance; only <see cref="Hash"/> is ever persisted (mirrors the same
/// algorithm as `Identity.DeviceSessions.DeviceSessionToken`, kept local to
/// this module rather than referenced across the module boundary for a
/// primitive this small — V0-ARC-001).
/// </summary>
public static class TableTokenGenerator
{
    private const string StorePrefix = "alkaros-table-token:";

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
