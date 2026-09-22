namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// In-memory <see cref="ISecretRotationStore"/> for tests and
/// single-process scenarios. Thread-safe: <see cref="Save"/> performs an
/// atomic compare-and-swap on <see cref="SecretRotationRecord.Version"/>
/// under a lock, so two threads racing a <c>Find</c> → mutate → <c>Save</c>
/// sequence against the same secret name cannot silently overwrite one
/// another — the second writer gets <see cref="SecretRotationConcurrencyException"/>.
/// </summary>
public sealed class InMemorySecretRotationStore : ISecretRotationStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SecretRotationRecord> _records = new(StringComparer.Ordinal);

    public SecretRotationRecord? Find(string secretName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        lock (_gate)
        {
            return _records.TryGetValue(secretName, out var record) ? record : null;
        }
    }

    public void Save(SecretRotationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            _records.TryGetValue(record.SecretName, out var existing);
            // No prior persisted state for this secret name: this is the
            // first-ever Save, so whatever local mutation chain the caller
            // built up before persisting (e.g. Initialize().Rotate()) is
            // not a concurrency conflict - there is nothing to conflict
            // with yet.
            if (existing is not null && record.Version != existing.Version + 1)
                throw new SecretRotationConcurrencyException(record.SecretName);

            _records[record.SecretName] = record;
        }
    }
}
