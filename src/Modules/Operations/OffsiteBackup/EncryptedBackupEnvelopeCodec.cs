namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Serializes an <see cref="EncryptedBackupEnvelope"/> to the flat byte
/// layout stored at the off-site target: header fields via
/// <see cref="BinaryWriter"/>/<see cref="BinaryReader"/>, ciphertext last
/// (unbounded length, no length prefix needed).
/// </summary>
public static class EncryptedBackupEnvelopeCodec
{
    public static byte[] Encode(EncryptedBackupEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(envelope.SecretName);
            writer.Write(envelope.KeyVersion);
            writer.Write(envelope.Nonce.Length);
            writer.Write(envelope.Nonce);
            writer.Write(envelope.Tag.Length);
            writer.Write(envelope.Tag);
            writer.Write(envelope.Ciphertext.Length);
            writer.Write(envelope.Ciphertext);
        }

        return stream.ToArray();
    }

    public static EncryptedBackupEnvelope Decode(ReadOnlyMemory<byte> encoded)
    {
        using var stream = new MemoryStream(encoded.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        var secretName = reader.ReadString();
        var keyVersion = reader.ReadInt32();
        var nonce = reader.ReadBytes(reader.ReadInt32());
        var tag = reader.ReadBytes(reader.ReadInt32());
        var ciphertext = reader.ReadBytes(reader.ReadInt32());

        return new EncryptedBackupEnvelope(secretName, keyVersion, nonce, ciphertext, tag);
    }
}
