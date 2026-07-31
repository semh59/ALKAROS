using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Validation;

public sealed class MigrationCompositionValidatorTests
{
    private static readonly MigrationFindingKind[] ExpectedFindingOrder =
    [
        MigrationFindingKind.MissingUp,
        MigrationFindingKind.MissingDown,
        MigrationFindingKind.NameMismatch,
        MigrationFindingKind.MissingUp,
        MigrationFindingKind.MissingDown,
        MigrationFindingKind.UnknownPosition,
    ];

    private static MigrationDiscoveryResult Discover(params (string FileName, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "alkaros-fnd004-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            foreach (var (fileName, content) in files)
                File.WriteAllText(Path.Combine(root, fileName), content);

            return MigrationDiscoverer.Discover(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MigrationManifest Manifest(params string[] ids)
    {
        var root = Path.Combine(Path.GetTempPath(), "alkaros-fnd004-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var path = TestMigrationSet.WriteManifest(root,
                ids.Select(id => new MigrationManifestEntry(id, TestMigrationSet.PhaseOf(id), [$"table_{id}"], [])).ToArray());
            return MigrationManifest.Load(path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompleteSetProducesNoFindings()
    {
        var manifest = Manifest("001", "002");
        var discovery = Discover(
            ("001-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("001-stores.down.sql", "DROP TABLE stores;"),
            ("002-printers.up.sql", "CREATE TABLE printers (id integer);"),
            ("002-printers.down.sql", "DROP TABLE printers;"));

        Assert.Empty(MigrationCompositionValidator.Validate(manifest, discovery));
    }

    [Fact]
    public void UnknownPositionIsRejectedBeforeExecution()
    {
        var manifest = Manifest("001", "002");
        var discovery = Discover(
            ("007-not-declared.up.sql", "CREATE TABLE x (id integer);"),
            ("007-not-declared.down.sql", "DROP TABLE x;"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.UnknownPosition && f.Position == "007");
    }

    [Fact]
    public void DuplicateUpScriptIsRejected()
    {
        var manifest = Manifest("001");
        var discovery = Discover(
            ("001-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("001-users.up.sql", "CREATE TABLE users (id integer);"),
            ("001-stores.down.sql", "DROP TABLE stores;"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.DuplicateUp && f.Position == "001");
    }

    [Fact]
    public void DuplicateDownScriptIsRejected()
    {
        var manifest = Manifest("001");
        var discovery = Discover(
            ("001-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("001-stores.down.sql", "DROP TABLE stores;"),
            ("001-users.down.sql", "DROP TABLE users;"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.DuplicateDown && f.Position == "001");
    }

    [Fact]
    public void MissingUpScriptIsRejected()
    {
        var manifest = Manifest("001", "002");
        var discovery = Discover(
            ("001-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("001-stores.down.sql", "DROP TABLE stores;"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.MissingUp && f.Position == "002");
    }

    [Fact]
    public void MissingDownScriptIsRejected()
    {
        var manifest = Manifest("001");
        var discovery = Discover(("001-stores.up.sql", "CREATE TABLE stores (id integer);"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.MissingDown && f.Position == "001");
    }

    [Fact]
    public void ForwardAndRollbackNameMismatchIsRejected()
    {
        var manifest = Manifest("001");
        var discovery = Discover(
            ("001-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("001-printers.down.sql", "DROP TABLE printers;"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.NameMismatch && f.Position == "001");
    }

    [Fact]
    public void UnrecognizedSqlFileIsRejected()
    {
        var manifest = Manifest("001");
        var discovery = Discover(
            ("001-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("001-stores.down.sql", "DROP TABLE stores;"),
            ("ad-hoc.sql", "SELECT 1;"));

        var findings = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Contains(findings, f => f.Kind == MigrationFindingKind.UnrecognizedSqlFile);
    }

    [Fact]
    public void FindingsAreDeterministicallyOrdered()
    {
        var manifest = Manifest("001", "002", "003");
        var discovery = Discover(
            ("002-stores.up.sql", "CREATE TABLE stores (id integer);"),
            ("002-other.down.sql", "DROP TABLE x;"),
            ("099-unknown.up.sql", "CREATE TABLE z (id integer);"));

        var first = MigrationCompositionValidator.Validate(manifest, discovery);
        var second = MigrationCompositionValidator.Validate(manifest, discovery);

        Assert.Equal(ExpectedFindingOrder, first.Select(f => f.Kind).ToArray());
        Assert.Equal(first, second);
    }
}
