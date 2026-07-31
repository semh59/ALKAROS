using System.Text.Json;
using ALKAROS.Host.Composition.Migrations;

namespace ALKAROS.Host.Tests.Fixtures;

public sealed record TestMigrationScript(
    string Id,
    string Phase,
    string Name,
    string? UpSql = null,
    string? DownSql = null)
{
    public static TestMigrationScript For(string id, string phase, string name,
        string? upSql = null, string? downSql = null)
        => new(id, phase, name, upSql, downSql);
}

/// <summary>
/// Writes a self-contained fixture migration set into a temporary directory:
/// an order.json manifest plus forward/rollback SQL scripts. Manifest
/// positions are always declared in ascending order.
/// </summary>
public sealed class TestMigrationSet : IDisposable
{
    private static readonly string[] Source = ["test-fixture"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _root;

    private TestMigrationSet(string root, MigrationManifest manifest)
    {
        _root = root;
        DirectoryPath = root;
        ManifestPath = Path.Combine(root, "order.json");
        Manifest = manifest;
    }

    public string DirectoryPath { get; }

    public string ManifestPath { get; }

    public MigrationManifest Manifest { get; }

    public static TestMigrationSet Create(params TestMigrationScript[] scripts)
        => Create(scripts.Select(s => s.Id).Distinct().ToArray(), scripts);

    public static TestMigrationSet Create(string[] manifestIds, params TestMigrationScript[] scripts)
    {
        var root = Path.Combine(Path.GetTempPath(), "alkaros-fnd004-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        foreach (var script in scripts)
        {
            if (script.UpSql is not null)
                File.WriteAllText(Path.Combine(root, $"{script.Id}-{script.Name}.up.sql"), script.UpSql);
            if (script.DownSql is not null)
                File.WriteAllText(Path.Combine(root, $"{script.Id}-{script.Name}.down.sql"), script.DownSql);
        }

        var entries = manifestIds
            .OrderBy(id => id)
            .Select(id => new MigrationManifestEntry(
                id,
                PhaseOf(id),
                [scripts.FirstOrDefault(s => s.Id == id)?.Name ?? $"table_{id}"],
                []))
            .ToArray();

        WriteManifest(root, entries);
        return new TestMigrationSet(root, MigrationManifest.Load(Path.Combine(root, "order.json")));
    }

    /// <summary>
    /// Writes arbitrary files verbatim into a fresh fixture directory; useful
    /// for contract-violation cases the high-level builder cannot express.
    /// </summary>
    public static TestMigrationSet CreateWithFiles(string[] manifestIds, params (string FileName, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "alkaros-fnd004-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        foreach (var (fileName, content) in files)
            File.WriteAllText(Path.Combine(root, fileName), content);

        var entries = manifestIds
            .OrderBy(id => id)
            .Select(id => new MigrationManifestEntry(id, PhaseOf(id), [$"table_{id}"], []))
            .ToArray();

        WriteManifest(root, entries);
        return new TestMigrationSet(root, MigrationManifest.Load(Path.Combine(root, "order.json")));
    }

    public static string WriteManifest(string directory, params MigrationManifestEntry[] entries)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "order.json");
        File.WriteAllText(path, SerializeManifest(entries));
        return path;
    }

    public static string SerializeManifest(IEnumerable<MigrationManifestEntry> entries)
        => JsonSerializer.Serialize(
            new
            {
                version = 1,
                source = Source,
                phaseARange = new { min = "001", max = "030" },
                phaseBRange = new { min = "031", max = "040" },
                migrations = entries.Select(e => new
                {
                    id = e.Id,
                    phase = e.Phase,
                    tables = e.Tables,
                    deferredConstraints = e.DeferredConstraints,
                }),
            },
            JsonOptions);

    public static string PhaseOf(string id)
        => string.CompareOrdinal(id, "030") <= 0 ? MigrationManifest.PhaseA : MigrationManifest.PhaseB;

    public static string DefaultUpSql(string tableName)
        => $"CREATE TABLE {tableName} (id integer PRIMARY KEY);";

    public static string DefaultDownSql(string tableName)
        => $"DROP TABLE IF EXISTS {tableName};";

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
