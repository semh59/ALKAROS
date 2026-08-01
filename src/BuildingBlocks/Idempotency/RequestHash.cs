using System.Security.Cryptography;

namespace ALKAROS.Idempotency;

/// <summary>
/// The canonical request hash of V0-ARC-003 §1: SHA-256 of the request
/// body, encoded as 64 lowercase hex characters.
/// </summary>
public static class RequestHash
{
    public static string Compute(ReadOnlyMemory<byte> requestBody)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(requestBody.Span, digest);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
