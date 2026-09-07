using System.Security.Cryptography;
using System.Text;

namespace ALKAROS.QrOrdering.CustomerSession;

/// <summary>
/// Generates opaque raw customer session tokens and their permanent SHA-256
/// representations — same algorithm as
/// <see cref="ALKAROS.QrOrdering.TokenLifecycle.TableTokenGenerator"/>, kept
/// as its own small type (rather than reused) because a customer session
/// token is a distinct credential with its own prefix, not a table token.
/// </summary>
public static class CustomerSessionTokenGenerator
{
    private const string StorePrefix = "alkaros-customer-session:";

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
