using System.Text.RegularExpressions;

namespace ALKAROS.Host.Composition.Migrations;

public enum MigrationScriptKind
{
    Up,
    Down,
}

/// <summary>
/// A migration script discovered under the migrations directory. The file
/// name contract is <c>&lt;position&gt;-&lt;name&gt;.up.sql</c> (forward) and
/// <c>&lt;position&gt;-&lt;name&gt;.down.sql</c> (rollback), where
/// <c>&lt;position&gt;</c> is the zero-padded three-digit id declared in the
/// migration manifest.
/// </summary>
public sealed record MigrationFile(string Id, string Name, MigrationScriptKind Kind, string Path)
{
    private static readonly Regex FileNamePattern = new(
        @"^([0-9]{3})-(.+)\.(up|down)\.sql$",
        RegexOptions.CultureInvariant);

    public static MigrationFile? TryParse(string fileName, string fullPath)
    {
        var match = FileNamePattern.Match(fileName);
        if (!match.Success)
            return null;

        return new MigrationFile(
            match.Groups[1].Value,
            match.Groups[2].Value,
            match.Groups[3].Value == "up" ? MigrationScriptKind.Up : MigrationScriptKind.Down,
            fullPath);
    }
}
