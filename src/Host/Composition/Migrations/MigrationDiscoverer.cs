namespace ALKAROS.Host.Composition.Migrations;

/// <summary>
/// Result of scanning the migrations directory. SQL files that do not match
/// the migration file name contract are reported separately so the
/// composition can reject them instead of silently skipping them.
/// </summary>
public sealed record MigrationDiscoveryResult(
    IReadOnlyList<MigrationFile> Files,
    IReadOnlyList<string> UnrecognizedSqlFiles);

public static class MigrationDiscoverer
{
    public static MigrationDiscoveryResult Discover(string migrationsRoot)
    {
        if (!Directory.Exists(migrationsRoot))
            throw new DirectoryNotFoundException(
                $"Migrations directory not found: '{migrationsRoot}'.");

        var files = new List<MigrationFile>();
        var unrecognized = new List<string>();

        foreach (var path in Directory.EnumerateFiles(migrationsRoot, "*.sql", SearchOption.AllDirectories))
        {
            var file = MigrationFile.TryParse(Path.GetFileName(path), path);
            if (file is null)
                unrecognized.Add(path);
            else
                files.Add(file);
        }

        files.Sort(static (a, b) =>
        {
            var byId = string.CompareOrdinal(a.Id, b.Id);
            return byId != 0 ? byId : a.Kind.CompareTo(b.Kind);
        });
        unrecognized.Sort(StringComparer.Ordinal);

        return new MigrationDiscoveryResult(files, unrecognized);
    }
}
