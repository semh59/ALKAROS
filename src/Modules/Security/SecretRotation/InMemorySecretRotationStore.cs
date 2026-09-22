namespace ALKAROS.Security.SecretRotation;

/// <summary>In-memory <see cref="ISecretRotationStore"/> for tests and single-process scenarios.</summary>
public sealed class InMemorySecretRotationStore : ISecretRotationStore
{
    private readonly Dictionary<string, SecretRotationRecord> _records = new(StringComparer.Ordinal);

    public SecretRotationRecord? Find(string secretName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        return _records.TryGetValue(secretName, out var record) ? record : null;
    }

    public void Save(SecretRotationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.SecretName] = record;
    }
}
