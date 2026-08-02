using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Manifest;

public sealed class ManifestTests : IDisposable
{
    private static readonly string[] FirstEntryTables = ["idempotency_keys"];
    private static readonly string[] FirstPhaseBEntryTables = ["invoices", "invoice_lines"];
    private static readonly string[] ExpectedDeferredConstraints =
    [
        "ALTER TABLE invoices ADD COLUMN fiscal_document_id UUID REFERENCES fiscal_documents(id);",
        "ALTER TABLE fiscal_documents ADD COLUMN invoice_id UUID REFERENCES invoices(id);",
        "ALTER TABLE invoices ADD COLUMN customer_account_id UUID REFERENCES customer_accounts(id);",
        "ALTER TABLE account_transactions ADD COLUMN invoice_id UUID REFERENCES invoices(id);",
    ];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "alkaros-fnd004-" + Guid.NewGuid().ToString("N")[..8]);

    public ManifestTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void RealManifestLoadsWithVerifiedOrderFromV0Dat001()
    {
        var manifest = MigrationManifest.Load(Path.Combine("Fixtures", "order.json"));

        Assert.Equal(31, manifest.Migrations.Count);
        Assert.Equal(26, manifest.Migrations.Count(e => e.Phase == MigrationManifest.PhaseA));
        Assert.Equal(5, manifest.Migrations.Count(e => e.Phase == MigrationManifest.PhaseB));
        Assert.Equal(
            FirstEntryTables,
            manifest.Migrations[0].Tables);
        Assert.Equal("021", manifest.Migrations[20].Id);
        Assert.Equal("035", manifest.Migrations[^1].Id);
        Assert.Equal(MigrationManifest.PhaseB, manifest.Migrations[26].Phase);
        Assert.Equal(FirstPhaseBEntryTables, manifest.Migrations[26].Tables);
        Assert.Equal(
            ExpectedDeferredConstraints,
            manifest.Migrations.Skip(27).SelectMany(e => e.DeferredConstraints).ToArray());
    }

    [Fact]
    public void ManifestLoadsWhenPositionsAreComplete()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry("001", MigrationManifest.PhaseA, "stores"),
            Entry("031", MigrationManifest.PhaseB, "invoices"));

        var manifest = MigrationManifest.Load(path);

        Assert.Equal(2, manifest.Migrations.Count);
    }

    [Fact]
    public void ManifestRejectsDuplicatePosition()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry("001", MigrationManifest.PhaseA, "stores"),
            Entry("001", MigrationManifest.PhaseA, "users"));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsPhaseAIdOutsideItsRange()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry("031", MigrationManifest.PhaseA, "stores"));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("outside range", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsPhaseBIdOutsideItsRange()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry("041", MigrationManifest.PhaseB, "invoices"));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("outside range", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsNonZeroPaddedPosition()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry("1", MigrationManifest.PhaseA, "stores"));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("three-digit", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsPhaseAEntryWithoutTables()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            new MigrationManifestEntry("001", MigrationManifest.PhaseA, [], []));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("at least one table", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsEmptyPhaseBEntry()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            new MigrationManifestEntry("031", MigrationManifest.PhaseB, [], []));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("deferred constraint", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsEmptyDeferredConstraint()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            new MigrationManifestEntry("031", MigrationManifest.PhaseB, [], [""]));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("empty deferred constraint", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsDescendingPositions()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry("002", MigrationManifest.PhaseA, "printers"),
            Entry("001", MigrationManifest.PhaseA, "stores"));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("strictly ascending", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsInvalidPhase()
    {
        var path = TestMigrationSet.WriteManifest(_directory,
            new MigrationManifestEntry("001", "C", ["stores"], []));

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("Invalid phase", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsMalformedJson()
    {
        var path = Path.Combine(_directory, "order.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ not json");

        Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
    }

    [Fact]
    public void ManifestRejectsUnsupportedVersion()
    {
        var path = Path.Combine(_directory, "order.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"version\":2,\"migrations\":[]}");

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("version 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestRejectsEmptyMigrationList()
    {
        var path = Path.Combine(_directory, "order.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"version\":1,\"migrations\":[]}");

        var ex = Assert.Throws<MigrationManifestException>(() => MigrationManifest.Load(path));
        Assert.Contains("at least one migration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestLoadThrowsWhenFileIsMissing()
    {
        Assert.Throws<MigrationManifestException>(() =>
            MigrationManifest.Load(Path.Combine(_directory, "missing.json")));
    }

    private static MigrationManifestEntry Entry(string id, string phase, string table)
        => new(id, phase, [table], []);
}
