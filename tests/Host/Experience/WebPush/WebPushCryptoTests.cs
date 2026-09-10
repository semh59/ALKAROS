using System.Security.Cryptography;
using System.Text;
using ALKAROS.Host.Experience.WebPush;
using Xunit;

namespace ALKAROS.Host.Experience.WebPush.Tests;

/// <summary>
/// V1-WTR-011: the encryption is checked against the worked example in
/// RFC 8291 §5, not against itself.
///
/// This matters more here than in most places. A push service accepts a
/// wrongly encrypted body with a 201 and says nothing; the browser then fails
/// to decrypt it and drops it silently. Without the RFC's own vector, a bug
/// anywhere in the HKDF/AES-GCM/header chain would look exactly like
/// "notifications are working" from the server's side.
/// </summary>
public sealed class WebPushCryptoTests
{
    // RFC 8291 §5, verbatim.
    private const string Plaintext = "When I grow up, I want to be a watermelon";
    private const string ReceiverPublicKey = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";
    private const string SenderPublicKey = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string SenderPrivateKey = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string ExpectedBody =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Fact]
    public void EncryptionReproducesTheRfc8291WorkedExample()
    {
        using var senderKey = WebPushCrypto.ImportKeyPair(
            WebPushCrypto.FromBase64Url(SenderPrivateKey),
            WebPushCrypto.FromBase64Url(SenderPublicKey));

        var body = WebPushCrypto.Encrypt(
            Plaintext, ReceiverPublicKey, AuthSecret, WebPushCrypto.FromBase64Url(Salt), senderKey);

        Assert.Equal(ExpectedBody, WebPushCrypto.ToBase64Url(body));
    }

    [Fact]
    public void EachMessageGetsItsOwnSaltAndEphemeralKey()
    {
        var first = WebPushCrypto.Encrypt(Plaintext, ReceiverPublicKey, AuthSecret);
        var second = WebPushCrypto.Encrypt(Plaintext, ReceiverPublicKey, AuthSecret);

        // Same plaintext, same subscription: identical output would mean the
        // salt or the ephemeral key was being reused, which RFC 8291 §3.1
        // forbids.
        Assert.NotEqual(WebPushCrypto.ToBase64Url(first), WebPushCrypto.ToBase64Url(second));
    }

    [Fact]
    public void APayloadOverTheRfcLimitIsRejectedRatherThanTruncated()
    {
        var tooLong = new string('a', WebPushCrypto.MaxPayloadBytes + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => WebPushCrypto.Encrypt(tooLong, ReceiverPublicKey, AuthSecret));
        Assert.Equal("payload", ex.ParamName);
    }

    [Fact]
    public void APayloadExactlyAtTheRfcLimitIsAccepted()
    {
        var atLimit = new string('a', WebPushCrypto.MaxPayloadBytes);

        var body = WebPushCrypto.Encrypt(atLimit, ReceiverPublicKey, AuthSecret);

        // 86-byte header + plaintext + 1 delimiter byte + 16-byte tag, which is
        // the 4096 a push service must accept.
        Assert.Equal(4096, body.Length);
    }

    [Fact]
    public void ASubscriptionKeyThatIsNotAPointIsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => WebPushCrypto.Encrypt(Plaintext, WebPushCrypto.ToBase64Url([1, 2, 3]), AuthSecret));
        Assert.Equal("clientPublicKey", ex.ParamName);
    }

    [Fact]
    public void TheVapidHeaderCarriesAVerifiableEs256SignatureOverTheStatedClaims()
    {
        var (publicKey, privateKey) = WebPushCrypto.CreateKeyPair();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        var header = WebPushCrypto.CreateVapidAuthorizationHeader(
            "https://push.example.net", "mailto:destek@alkaros.local", publicKey, privateKey, now);

        Assert.StartsWith("vapid t=", header, StringComparison.Ordinal);
        Assert.EndsWith($", k={publicKey}", header, StringComparison.Ordinal);

        var token = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)];
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        var claims = Encoding.UTF8.GetString(WebPushCrypto.FromBase64Url(parts[1]));
        Assert.Contains("\"aud\":\"https://push.example.net\"", claims, StringComparison.Ordinal);
        Assert.Contains("\"sub\":\"mailto:destek@alkaros.local\"", claims, StringComparison.Ordinal);
        // RFC 8292 §2 caps the lifetime at 24 hours.
        Assert.Contains($"\"exp\":{now.AddHours(12).ToUnixTimeSeconds()}", claims, StringComparison.Ordinal);

        // The push service verifies exactly this: the raw r||s signature over
        // "header.body" against the key advertised in k=.
        var point = WebPushCrypto.FromBase64Url(publicKey);
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..] },
        });
        Assert.True(verifier.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            WebPushCrypto.FromBase64Url(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc123", "https://fcm.googleapis.com")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAA", "https://updates.push.services.mozilla.com")]
    public void TheAudienceIsTheServiceOriginAndNeverTheFullEndpoint(string endpoint, string expected)
    {
        Assert.Equal(expected, WebPushCrypto.AudienceOf(endpoint));
    }
}
