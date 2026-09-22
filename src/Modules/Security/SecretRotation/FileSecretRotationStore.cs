using System.Text.Json;
using System.Text.RegularExpressions;

namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// File-based <see cref="ISecretRotationStore"/>: one JSON file per secret
/// name under a directory (matches the "outside the repo, deployment-owned"
/// storage model <c>deployment/secrets/**</c> already uses for the raw
/// secret values themselves — this file carries only version metadata,
/// never a value). Writes are atomic: a temp file is written first, then
/// moved into place, so a crash mid-write cannot corrupt the last-known-good
/// state.
/// </summary>
public sealed class FileSecretRotationStore : ISecretRotationStore
{
    private static readonly Regex SafeSecretName = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

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
        if (!File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        var dto = JsonSerializer.Deserialize<StoredRecord>(stream)
            ?? throw new InvalidDataException($"Rotation state file for '{secretName}' deserialized to null.");
        return SecretRotationRecord.Restore(dto.SecretName, dto.Versions);
    }

    public void Save(SecretRotationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var path = PathFor(record.SecretName);
        var tempPath = path + ".tmp";
        var dto = new StoredRecord(record.SecretName, record.Versions.ToArray());
        File.WriteAllText(tempPath, JsonSerializer.Serialize(dto, SerializerOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private string PathFor(string secretName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        if (!SafeSecretName.IsMatch(secretName))
            throw new ArgumentException(
                "Secret name must match ^[a-z0-9][a-z0-9-]*$ to be usable as a file name.", nameof(secretName));
        return Path.Combine(_directory, secretName + ".json");
    }

    private sealed record StoredRecord(string SecretName, SecretVersionRecord[] Versions);
}
