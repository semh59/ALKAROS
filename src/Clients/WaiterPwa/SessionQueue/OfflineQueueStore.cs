using System.Text.Json;

namespace ALKAROS.Clients.WaiterPwa.SessionQueue;

public interface IOfflineQueueStore
{
    IReadOnlyList<QueuedOperation> Load();

    void Save(IReadOnlyList<QueuedOperation> operations);
}

public sealed class InMemoryOfflineQueueStore : IOfflineQueueStore
{
    private IReadOnlyList<QueuedOperation> _operations = [];

    public IReadOnlyList<QueuedOperation> Load() => _operations.ToArray();

    public void Save(IReadOnlyList<QueuedOperation> operations)
        => _operations = operations.ToArray();
}

public sealed class JsonFileOfflineQueueStore : IOfflineQueueStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path;

    public JsonFileOfflineQueueStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public IReadOnlyList<QueuedOperation> Load()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<QueuedOperation[]>(File.ReadAllText(_path), SerializerOptions)
                ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Offline queue store '{_path}' contains invalid JSON.", exception);
        }
    }

    public void Save(IReadOnlyList<QueuedOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException($"Offline queue store '{_path}' has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(operations, SerializerOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
