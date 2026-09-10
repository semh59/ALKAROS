using System.Security.Cryptography;
using System.Text;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>
/// V1-WTR-011: RFC 8291 (Message Encryption for Web Push) and the base64url
/// alphabet RFC 8292 uses, written against System.Security.Cryptography only.
///
/// No package was added for this. .NET 8 ships every primitive the two RFCs
/// need - <see cref="ECDiffieHellman"/> over P-256, <see cref="HKDF"/>,
/// <see cref="AesGcm"/> and <see cref="ECDsa"/> for ES256 - and this
/// repository keeps a deliberately small dependency set
/// (Directory.Packages.props), so pulling in a web-push library to wrap
/// primitives that are already in the box would be the larger change.
/// </summary>
public static class WebPushCrypto
{
    /// <summary>
    /// RFC 8291 §4: the largest plaintext that still fits a push service's
    /// guaranteed 4096-byte body once the record header (86) and the AEAD
    /// tag plus padding delimiter (17) are accounted for.
    /// </summary>
    public const int MaxPayloadBytes = 3993;

    private const int SaltBytes = 16;
    private const int KeyBytes = 16;
    private const int NonceBytes = 12;
    private const int UncompressedPointBytes = 65;

    /// <summary>
    /// Encrypts <paramref name="payload"/> for one subscription and returns
    /// the complete aes128gcm body: the RFC 8188 header (salt, record size,
    /// the server's ephemeral public key) followed by a single record.
    /// </summary>
    /// <param name="clientPublicKey">The subscription's p256dh value, base64url.</param>
    /// <param name="clientAuthSecret">The subscription's auth value, base64url.</param>
    public static byte[] Encrypt(string payload, string clientPublicKey, string clientAuthSecret)
    {
        using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Encrypt(
            payload,
            clientPublicKey,
            clientAuthSecret,
            RandomNumberGenerator.GetBytes(SaltBytes),
            serverKey);
    }

    /// <summary>
    /// The same encryption with the two random inputs supplied by the caller.
    /// It exists so the worked example in RFC 8291 §5 can be reproduced
    /// byte for byte in a test — an encryption bug here would otherwise be
    /// invisible, since a push service rejects nothing and the device simply
    /// never shows a notification.
    /// </summary>
    /// <remarks>
    /// Never call this with a reused salt or a reused key pair outside a test:
    /// RFC 8291 §3.1 requires both to be fresh for every message.
    /// </remarks>
    public static byte[] Encrypt(
        string payload,
        string clientPublicKey,
        string clientAuthSecret,
        byte[] salt,
        ECDiffieHellman serverKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(serverKey);

        var plaintext = Encoding.UTF8.GetBytes(payload);
        if (plaintext.Length > MaxPayloadBytes)
            throw new ArgumentException(
                $"A web push payload may not exceed {MaxPayloadBytes} bytes.", nameof(payload));

        var clientPublic = FromBase64Url(clientPublicKey);
        var authSecret = FromBase64Url(clientAuthSecret);
        if (clientPublic.Length != UncompressedPointBytes)
            throw new ArgumentException("The subscription key is not an uncompressed P-256 point.", nameof(clientPublicKey));

        var serverPublic = ExportUncompressedPoint(serverKey);
        using var clientKey = ImportUncompressedPoint(clientPublic);

        // RFC 8291 §3.3. The shared secret is stretched with the auth secret
        // first, using a context that binds both public keys, so a key pair
        // reused across subscriptions still yields distinct content keys.
        var sharedSecret = serverKey.DeriveRawSecretAgreement(clientKey.PublicKey);

        var keyInfo = Concat(
            Encoding.ASCII.GetBytes("WebPush: info\0"),
            clientPublic,
            serverPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);
        CryptographicOperations.ZeroMemory(sharedSecret);

        var contentEncryptionKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, ikm, KeyBytes, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, ikm, NonceBytes, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        CryptographicOperations.ZeroMemory(ikm);

        // RFC 8188 §2: the record's plaintext ends with a delimiter, 0x02 for
        // the last (here only) record.
        var record = new byte[plaintext.Length + 1];
        plaintext.CopyTo(record, 0);
        record[^1] = 0x02;

        var ciphertext = new byte[record.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(contentEncryptionKey, tag.Length))
        {
            aes.Encrypt(nonce, record, ciphertext, tag);
        }
        CryptographicOperations.ZeroMemory(contentEncryptionKey);
        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(record);

        // RFC 8188 §2.1 header: salt | record size (uint32 BE) | key id length
        // | key id. For web push the key id is the server's public key.
        //
        // "rs" is the record size the sender *declares*, not the length of
        // what it actually sent: a receiver uses it to size its buffers, and
        // a single short final record is legal. Writing the real length here
        // instead produced a body that every push service accepts with a 201
        // and no browser can decrypt — caught only because RFC 8291 §5's own
        // worked example is asserted in WebPushCryptoTests.
        const int recordSize = 4096;
        var header = new byte[salt.Length + 4 + 1 + serverPublic.Length];
        salt.CopyTo(header, 0);
        header[salt.Length] = (byte)((recordSize >> 24) & 0xFF);
        header[salt.Length + 1] = (byte)((recordSize >> 16) & 0xFF);
        header[salt.Length + 2] = (byte)((recordSize >> 8) & 0xFF);
        header[salt.Length + 3] = (byte)(recordSize & 0xFF);
        header[salt.Length + 4] = (byte)serverPublic.Length;
        serverPublic.CopyTo(header, salt.Length + 5);

        return Concat(header, ciphertext, tag);
    }

    /// <summary>Creates a fresh P-256 pair as a (public, private) base64url pair.</summary>
    public static (string PublicKey, string PrivateKey) CreateKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);
        var publicPoint = new byte[UncompressedPointBytes];
        publicPoint[0] = 0x04;
        parameters.Q.X!.CopyTo(publicPoint, 1);
        parameters.Q.Y!.CopyTo(publicPoint, 33);
        return (ToBase64Url(publicPoint), ToBase64Url(parameters.D!));
    }

    /// <summary>
    /// RFC 8292 §2: the signed JWT that identifies this server to the push
    /// service, plus the public key the service checks it against.
    /// </summary>
    public static string CreateVapidAuthorizationHeader(
        string audience, string subject, string publicKey, string privateKey, DateTimeOffset now)
    {
        var header = ToBase64Url(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));

        // RFC 8292 §2 caps the lifetime at 24 hours; 12 leaves room for a
        // device whose clock is off without ever being rejected as stale.
        var expiry = now.AddHours(12).ToUnixTimeSeconds();
        var claims = $$"""{"aud":"{{audience}}","exp":{{expiry}},"sub":"{{subject}}"}""";
        var body = ToBase64Url(Encoding.UTF8.GetBytes(claims));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{body}");

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url(privateKey),
            Q = PointFromUncompressed(FromBase64Url(publicKey)),
        });

        // IeeeP1363 is the raw r||s pair JWS ES256 requires, not DER.
        var signature = key.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var token = $"{header}.{body}.{ToBase64Url(signature)}";
        return $"vapid t={token}, k={publicKey}";
    }

    /// <summary>
    /// The origin a push endpoint belongs to - the "aud" claim RFC 8292
    /// requires, which is the service's origin and never the full path.
    /// </summary>
    public static string AudienceOf(string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.Absolute);
        return $"{uri.Scheme}://{uri.Authority}";
    }

    public static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            0 => padded,
            _ => throw new FormatException("Value is not valid base64url."),
        };
        return Convert.FromBase64String(padded);
    }

    private static byte[] ExportUncompressedPoint(ECDiffieHellman key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var point = new byte[UncompressedPointBytes];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);
        return point;
    }

    /// <summary>
    /// Rebuilds a P-256 agreement key from the raw scalar and point the RFC
    /// test vector states, which is the only way to reproduce a fixed
    /// ephemeral key.
    /// </summary>
    public static ECDiffieHellman ImportKeyPair(byte[] privateScalar, byte[] publicPoint) =>
        ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = privateScalar,
            Q = PointFromUncompressed(publicPoint),
        });

    private static ECDiffieHellman ImportUncompressedPoint(byte[] point) =>
        ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = PointFromUncompressed(point),
        });

    private static ECPoint PointFromUncompressed(byte[] point)
    {
        if (point.Length != UncompressedPointBytes || point[0] != 0x04)
            throw new FormatException("Expected a 65-byte uncompressed P-256 point.");
        return new ECPoint { X = point[1..33], Y = point[33..] };
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }
}
