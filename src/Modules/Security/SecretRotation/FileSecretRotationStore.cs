using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// File-based <see cref="ISecretRotationStore"/>: one JSON file per secret
/// name under a directory (matches the "outside the repo, deployment-owned"
/// storage model <c>deployment/secrets/**</c> already uses for the raw
/// secret values themselves — this file carries only version metadata,
/// never a value). Writes are atomic: a temp file — named with a random
/// GUID suffix so two concurrent <see cref="Save"/> calls for the same
/// secret name can never collide on the same temp path — is written first,
/// then moved into place, so a crash mid-write cannot corrupt the
/// last-known-good state. <see cref="Save"/> also enforces optimistic
/// concurrency (see <see cref="ISecretRotationStore.Save"/>): the
/// read-check-write sequence is serialized per file path under an
/// in-process lock, so a racing pair of <c>Find</c> → mutate → <c>Save</c>
/// calls for the same secret name cannot silently overwrite one another.
/// </summary>
public sealed class FileSecretRotationStore : ISecretRotationStore
{
    private static readonly Regex SafeSecretName = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    // Per-path advisory locks: guards the read-check-write sequence in
    // Save so two threads racing to persist the same secret name cannot
    // both pass the version check before either writes. Keyed by the
    // full file path so stores over different directories never contend.
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _directory;

    public FileSecretRotationStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public SecretRotationRecord? Find(string secretName)
    {
        var path = PathFor(secretName);
        var stored = ReadStoredRecord(path);
        return stored is null ? null : SecretRotationRecord.Restore(stored.SecretName, stored.Versions, stored.Version);
    }

    public void Save(SecretRotationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var path = PathFor(record.SecretName);

        lock (PathLocks.GetOrAdd(path, static _ => new object()))
        {
            var existing = ReadStoredRecord(path);
            // No prior persisted file for this secret name: this is the
            // first-ever Save, so whatever local mutation chain the caller
            // built up before persisting (e.g. Initialize().Rotate()) is
            // not a concurrency conflict - there is nothing to conflict
            // with yet.
            if (existing is not null && record.Version != existing.Version + 1)
                throw new SecretRotationConcurrencyException(record.SecretName);

            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var dto = new StoredRecord(record.SecretName, record.Versions.ToArray(), record.Version);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(dto, SerializerOptions));
            File.Move(tempPath, path, overwrite: true);
        }
    }

    private static StoredRecord? ReadStoredRecord(string path)
    {
        if (!File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<StoredRecord>(stream)
            ?? throw new InvalidDataException($"Rotation state file at '{path}' deserialized to null.");
    }

    private string PathFor(string secretName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        if (!SafeSecretName.IsMatch(secretName))
            throw new ArgumentException(
                "Secret name must match ^[a-z0-9][a-z0-9-]*$ to be usable as a file name.", nameof(secretName));
        return Path.Combine(_directory, secretName + ".json");
    }

    // Version defaults to 1 so files written before this field existed
    // (which never carried a version) are treated as the first persisted
    // state rather than failing to deserialize.
    private sealed record StoredRecord(string SecretName, SecretVersionRecord[] Versions, int Version = 1);
}
