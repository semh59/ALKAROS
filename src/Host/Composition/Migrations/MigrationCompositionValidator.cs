namespace ALKAROS.Host.Composition.Migrations;

public enum MigrationFindingKind
{
    UnknownPosition,
    MissingUp,
    DuplicateUp,
    MissingDown,
    DuplicateDown,
    NameMismatch,
    UnrecognizedSqlFile,
}

/// <summary>
/// A fail-closed composition finding. Any finding prevents the whole
/// migration run: no SQL is executed when the discovered set deviates from
/// the verified global order.
/// </summary>
public sealed record MigrationFinding(MigrationFindingKind Kind, string Position, string Detail)
{
    public override string ToString()
        => $"{Kind} position='{Position}' detail='{Detail}'";
}

/// <summary>
/// Validates discovered migration files against the manifest before anything
/// is executed. Rules:
/// <list type="bullet">
/// <item>every file position must be declared in the manifest (order violation otherwise);</item>
/// <item>every manifest position needs exactly one forward and one rollback script;</item>
/// <item>duplicate scripts for the same position are rejected;</item>
/// <item>forward and rollback scripts at the same position must share the same name;</item>
/// <item>SQL files that do not match the file name contract are rejected.</item>
/// </list>
/// </summary>
public static class MigrationCompositionValidator
{
    public static IReadOnlyList<MigrationFinding> Validate(
        MigrationManifest manifest,
        MigrationDiscoveryResult discovery)
    {
        var findings = new List<MigrationFinding>();

        foreach (var path in discovery.UnrecognizedSqlFiles)
            findings.Add(new MigrationFinding(
                MigrationFindingKind.UnrecognizedSqlFile, string.Empty, path));

        var ups = discovery.Files.Where(f => f.Kind == MigrationScriptKind.Up).ToList();
        var downs = discovery.Files.Where(f => f.Kind == MigrationScriptKind.Down).ToList();
        var manifestPositions = manifest.Migrations.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);

        ValidateScripts(findings, ups, manifestPositions, MigrationFindingKind.DuplicateUp);
        ValidateScripts(findings, downs, manifestPositions, MigrationFindingKind.DuplicateDown);

        foreach (var entry in manifest.Migrations)
        {
            if (!ups.Any(f => f.Id == entry.Id))
                findings.Add(new MigrationFinding(
                    MigrationFindingKind.MissingUp, entry.Id, "No forward script declares this position."));

            if (!downs.Any(f => f.Id == entry.Id))
                findings.Add(new MigrationFinding(
                    MigrationFindingKind.MissingDown, entry.Id, "No rollback script declares this position."));

            var up = ups.FirstOrDefault(f => f.Id == entry.Id);
            var down = downs.FirstOrDefault(f => f.Id == entry.Id);
            if (up is not null && down is not null && !string.Equals(up.Name, down.Name, StringComparison.Ordinal))
                findings.Add(new MigrationFinding(
                    MigrationFindingKind.NameMismatch, entry.Id,
                    $"Forward script '{up.Name}' does not match rollback script '{down.Name}'."));
        }

        return findings
            .OrderBy(f => f.Position)
            .ThenBy(f => f.Kind)
            .ThenBy(f => f.Detail, StringComparer.Ordinal)
            .ToList();
    }

    private static void ValidateScripts(
        List<MigrationFinding> findings,
        List<MigrationFile> scripts,
        HashSet<string> manifestPositions,
        MigrationFindingKind duplicateKind)
    {
        foreach (var group in scripts.GroupBy(f => f.Id))
        {
            if (!manifestPositions.Contains(group.Key))
            {
                foreach (var file in group)
                    findings.Add(new MigrationFinding(
                        MigrationFindingKind.UnknownPosition, file.Id,
                        $"Position is not declared in the verified migration order; file: {file.Path}"));
                continue;
            }

            if (group.Count() > 1)
                findings.Add(new MigrationFinding(
                    duplicateKind, group.Key,
                    string.Join(", ", group.Select(f => f.Path))));
        }
    }
}
