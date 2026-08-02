using System.Security.Cryptography;

namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Hashes and verifies passwords with PBKDF2-HMAC-SHA256 using a per-password
/// random salt. The stored representation is self-describing so iteration
/// upgrades stay backward compatible:
/// <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>.
/// </summary>
public sealed class PasswordHasher
{
    public const int DefaultIterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string AlgorithmTag = "pbkdf2-sha256";
    private const int MinimumIterations = 10_000;

    private readonly int _iterations;

    public PasswordHasher(int iterations = DefaultIterations)
    {
        if (iterations < MinimumIterations)
            throw new ArgumentOutOfRangeException(nameof(iterations), iterations, "Too few iterations.");
        _iterations = iterations;
    }

    /// <summary>
    /// Produces the self-describing salted hash of <paramref name="password"/>.
    /// </summary>
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
            throw new ArgumentException("Password must not be empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, _iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{AlgorithmTag}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies <paramref name="password"/> against a previously produced
    /// encoded hash. The encoded hash carries its own iteration count, so
    /// re-hashing with the current default is never required.
    /// </summary>
    public static bool Verify(string password, string encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(encodedHash);

        if (!TryParse(encodedHash, out var iterations, out var salt, out var expectedHash))
            return false;

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool TryParse(
        string encoded,
        out int iterations,
        out byte[] salt,
        out byte[] expectedHash)
    {
        iterations = 0;
        salt = [];
        expectedHash = [];

        var parts = encoded.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], AlgorithmTag, StringComparison.Ordinal))
            return false;
        if (!int.TryParse(parts[1], out iterations) || iterations < MinimumIterations)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length == SaltSize && expectedHash.Length == HashSize;
    }
}
