using System.Security.Cryptography;
using System.Text;

namespace ALKAROS.Host.DualScreen;

public static class DualScreenToken
{
    public static (string Raw, string Hash) Create(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Token prefix cannot be empty.", nameof(prefix));
        var raw = prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (raw, Hash(raw));
    }

    public static string Hash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Token value cannot be empty.", nameof(value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static bool Matches(string raw, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var actual = Convert.FromHexString(Hash(raw));
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
