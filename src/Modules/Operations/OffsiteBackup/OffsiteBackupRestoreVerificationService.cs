using System.Security.Cryptography;

namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Confirms an off-site artifact is actually recoverable: download,
/// decrypt with the authorized key, and match the plaintext checksum
/// against the receipt. This is a verification primitive, not restore
/// orchestration (V15-BKP-002's own scope) — it never writes the plaintext
/// back into a running database.
/// </summary>
public sealed class OffsiteBackupRestoreVerificationService
{
    private readonly IOffsiteBackupTarget _target;
    private readonly OffsiteBackupCipher _cipher;

    public OffsiteBackupRestoreVerificationService(IOffsiteBackupTarget target, OffsiteBackupCipher cipher)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
    }

    /// <summary>
    /// Downloads and decrypts the artifact behind <paramref name="receipt"/>.
    /// Throws <see cref="OffsiteBackupDecryptionFailedException"/> if the
    /// authorized key cannot recover it, and
    /// <see cref="OffsiteBackupSourceIntegrityException"/> if the decrypted
    /// plaintext no longer matches the checksum recorded at upload time.
    /// </summary>
    public async Task<byte[]> DownloadAndVerifyAsync(OffsiteBackupReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var encoded = await _target.DownloadAsync(receipt.ArtifactId, cancellationToken);
        var envelope = EncryptedBackupEnvelopeCodec.Decode(encoded);
        var plaintext = _cipher.Decrypt(receipt.ArtifactId, receipt.DataClass, envelope);

        var actualChecksum = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
        if (!string.Equals(actualChecksum, receipt.PlaintextChecksumSha256, StringComparison.Ordinal))
            throw new OffsiteBackupSourceIntegrityException(receipt.ArtifactId);

        return plaintext;
    }
}
