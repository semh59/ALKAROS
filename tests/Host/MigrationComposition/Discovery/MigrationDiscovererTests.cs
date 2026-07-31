using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Discovery;

public sealed class MigrationDiscovererTests : IDisposable
{
    private static readonly string[] NestedFileIds = ["001", "002", "002"];
    private static readonly string[] SortedResultIds = ["001", "002"];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "alkaros-fnd004-" + Guid.NewGuid().ToString("N")[..8]);

    public MigrationDiscovererTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void DiscoversUpAndDownScriptsInNestedDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "nested"));
        File.WriteAllText(Path.Combine(_root, "001-stores.up.sql"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "nested", "002-printers.down.sql"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "nested", "002-printers.up.sql"), string.Empty);

        var result = MigrationDiscoverer.Discover(_root);

        Assert.Equal(3, result.Files.Count);
        Assert.Equal(NestedFileIds, result.Files.Select(f => f.Id));
        Assert.Equal(
            new[] { MigrationScriptKind.Up, MigrationScriptKind.Up, MigrationScriptKind.Down },
            result.Files.Select(f => f.Kind));
        Assert.Empty(result.UnrecognizedSqlFiles);
    }

    [Fact]
    public void ParsesScriptKindAndName()
    {
        var up = MigrationFile.TryParse("012-stock_ledger_entries.up.sql", "C:\\x\\012-stock_ledger_entries.up.sql");
        var down = MigrationFile.TryParse("001-stores.down.sql", "C:\\x\\001-stores.down.sql");

        Assert.NotNull(up);
        Assert.Equal("012", up.Id);
        Assert.Equal("stock_ledger_entries", up.Name);
        Assert.Equal(MigrationScriptKind.Up, up.Kind);
        Assert.NotNull(down);
        Assert.Equal(MigrationScriptKind.Down, down.Kind);
    }

    [Fact]
    public void ReportsSqlFilesThatDoNotMatchTheContract()
    {
        File.WriteAllText(Path.Combine(_root, "notes.sql"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "001-x.UP.sql"), string.Empty);

        var result = MigrationDiscoverer.Discover(_root);

        Assert.Empty(result.Files);
        Assert.Equal(2, result.UnrecognizedSqlFiles.Count);
        Assert.Contains(result.UnrecognizedSqlFiles, p => p.EndsWith("notes.sql", StringComparison.Ordinal));
        Assert.Contains(result.UnrecognizedSqlFiles, p => p.EndsWith("001-x.UP.sql", StringComparison.Ordinal));
    }

    [Fact]
    public void IgnoresNonSqlFiles()
    {
        File.WriteAllText(Path.Combine(_root, "README.md"), string.Empty);

        var result = MigrationDiscoverer.Discover(_root);

        Assert.Empty(result.Files);
        Assert.Empty(result.UnrecognizedSqlFiles);
    }

    [Fact]
    public void ThrowsWhenDirectoryIsMissing()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            MigrationDiscoverer.Discover(Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public void ResultsAreDeterministicAndSortedById()
    {
        File.WriteAllText(Path.Combine(_root, "002-b.up.sql"), string.Empty);
        File.WriteAllText(Path.Combine(_root, "001-a.up.sql"), string.Empty);

        var first = MigrationDiscoverer.Discover(_root);
        var second = MigrationDiscoverer.Discover(_root);

        Assert.Equal(SortedResultIds, first.Files.Select(f => f.Id));
        Assert.Equal(first.Files.Select(f => f.Id), second.Files.Select(f => f.Id));
        Assert.Equal(first.UnrecognizedSqlFiles, second.UnrecognizedSqlFiles);
    }
}
