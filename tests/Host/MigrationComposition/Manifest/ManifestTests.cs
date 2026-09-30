using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Manifest;

public sealed class ManifestTests : IDisposable
{
    private static readonly string[] FirstEntryTables = ["idempotency_keys"];
    private static readonly string[] RuntimeManifestIds =
    [
        "001", "002", "003", "005", "006", "007", "008", "009", "010", "011", "012", "013", "014", "015", "016", "017", "018", "019", "020", "021", "022", "023", "024", "025", "026", "027", "028", "029", "030", "031", "032", "033", "034", "035", "036", "037", "038", "039", "040", "041", "042", "043", "044", "045", "046", "047", "048", "049", "050", "051", "052", "053", "054", "055", "056", "057", "058", "059", "060", "061", "062", "063", "064", "065", "066", "067", "068", "069", "070", "071", "072", "073", "074", "075", "076", "077", "078", "079", "080", "081", "082", "083", "084", "085", "086", "087", "088", "089", "090", "091", "092", "093", "094", "095", "096", "097", "098", "099", "100", "101", "102", "103", "104", "105", "106", "107", "108", "109", "110", "111", "112", "113", "114", "115", "116", "117", "118", "119", "120", "121", "122", "123", "124", "125", "126", "127", "128", "129", "130", "131", "132", "133", "134", "135", "136", "137", "138", "139", "140", "141", "142", "143", "144", "145", "146", "147", "148", "149", "150", "151", "152", "153", "154", "155", "156", "157", "158", "159", "160", "161", "162", "163", "164", "165", "166", "167", "168", "169",
    ];
    // V1-RMD-244: this literal (and RuntimeManifestIds/the count below) has
    // now gone stale THREE times (098 in V1-RMD-157, 099 in V1-WTR-012, and
    // everything from 126 onward here) — each real new migration position
    // must update this test in the same diff, same discipline as
    // MigrationManifest.PhaseBMax's own doc-comment already demands.
    // V1-RMD-258: updated again for migration 141 (card_settlement_attempts
    // bill_id backfill + allocation_id unique index).
    // V1-RMD-266: and again for migration 142 (security.manage permission).
    // V1-RMD-283: and once more for migration 143 (manual_card_confirmations).
    // V12-MAP-001: and for migration 144 (yemeksepeti_product_mappings).
    // V12-ONL-001: and for migration 145 (yemeksepeti_webhook_inbox).
    // V12-ONL-002: and for migration 146 (processing columns on the same inbox table).
    // V12-ONL-004: and for migration 147 (catalog publications and their items).
    // V12-ONL-005: and for migration 148 (online availability states).
    // V12-REC-001: and for migration 149 (online order reconciliation retries).
    // V12-RMD-004: and for migration 150 (intake retry wait, one order per provider order).
    // V12-RMD-007: and for migration 151 (customer note scrubbed from online orders).
    // V12-OUI-003: and for migration 155 (online platform settings entered from the interface).
    // V12-ONL-009: and for migration 156 (per-platform order polling state).
    // V12-ONL-011: and for migration 157 (per-platform open/closed request).
    // V1-RMD-359: and for migration 158 (manual_card_confirmations decided-consistency constraint fix).
    // V14-CST-001: and for migration 159 (customer_data.profiles).
    // V14-CST-002: and for migration 160 (customer_data.anonymization_requests).
    // V14-ACC-001: and for migration 161 (customer_account.account_transactions).
    // V14-ACC-002: and for migration 162 (customer_account.balances / balance_snapshots).
    // V1-RMD-401: and for migration 163 (payments.take permission seed).
    // V1-RMD-440: and for migration 164 (customer_account.credit_terms).
    // V14-ACC-004: and for migration 165 (customer_account.account_payments / status history).
    // V14-ACC-009: and for migration 166 (customer_account.account_receipts).
    // And for migration 169 (invoicing.invoice_line_sources).
    private static readonly string[] LastEntryTables = ["invoice_line_sources"];
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
    public void RuntimeManifestContainsOnlyImplementedMigrationPairs()
    {
        var manifest = MigrationManifest.Load(Path.Combine("Fixtures", "order.json"));

        Assert.Equal(168, manifest.Migrations.Count);
        Assert.Equal(RuntimeManifestIds, manifest.Migrations.Select(entry => entry.Id));
        Assert.Equal(
            FirstEntryTables,
            manifest.Migrations[0].Tables);
        Assert.Equal(LastEntryTables, manifest.Migrations[^1].Tables);
    }

    [Fact]
    public void ExtensionConsumerAndOwnershipAssertionRemainStrictlyOrdered()
    {
        var manifest = MigrationManifest.Load(Path.Combine("Fixtures", "order.json"));

        var migrations = manifest.Migrations.ToList();
        var catalogIndex = migrations.FindIndex(entry => entry.Id == "007");
        var ownershipAssertionIndex = migrations.FindIndex(entry => entry.Id == "012");

        Assert.True(catalogIndex >= 0);
        Assert.True(ownershipAssertionIndex > catalogIndex);
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
        // One past PhaseBMax, whatever that currently is - a literal here
        // has twice become a real migration (098 in V1-RMD-157, 099 in
        // V1-WTR-012) and silently stopped testing what it claimed to.
        var onePastMax = (int.Parse(MigrationManifest.PhaseBMax, System.Globalization.CultureInfo.InvariantCulture) + 1)
            .ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
        var path = TestMigrationSet.WriteManifest(_directory,
            Entry(onePastMax, MigrationManifest.PhaseB, "invoices"));

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
