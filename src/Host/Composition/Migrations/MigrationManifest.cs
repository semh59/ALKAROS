using System.Text.Json;

namespace ALKAROS.Host.Composition.Migrations;

/// <summary>
/// One position of the verified global migration order. Positions are
/// zero-padded three-digit ids inside the phase ranges defined by
/// V0-DAT-001 (phase A: 001-030, phase B: 031-040).
/// </summary>
public sealed record MigrationManifestEntry(
    string Id,
    string Phase,
    IReadOnlyList<string> Tables,
    IReadOnlyList<string> DeferredConstraints)
{
    public bool IsPhaseB => Phase == MigrationManifest.PhaseB;
}

/// <summary>
/// The verified global migration order catalog
/// (docs/data/migration-dependency-graph.md, V0-DAT-001 + CORR:C1). The
/// manifest is the single source of truth for which migration positions are
/// allowed and in which order they must run. Anything not declared here is
/// rejected before any SQL is executed.
/// </summary>
public sealed class MigrationManifest
{
    public const int Version = 1;
    public const string PhaseA = "A";
    public const string PhaseB = "B";
    public const string PhaseAMin = "001";
    public const string PhaseAMax = "030";
    public const string PhaseBMin = "031";
    public const string PhaseBMax = "040";

    private MigrationManifest(IReadOnlyList<MigrationManifestEntry> migrations)
    {
        Migrations = migrations;
    }

    public IReadOnlyList<MigrationManifestEntry> Migrations { get; }

    public static MigrationManifest Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new MigrationManifestException($"Cannot read manifest '{path}': {ex.Message}");
        }

        ManifestDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, JsonOptions)
                ?? throw new JsonException("manifest is empty");
        }
        catch (JsonException ex)
        {
            throw new MigrationManifestException($"Malformed manifest '{path}': {ex.Message}");
        }

        Validate(dto);
        return new MigrationManifest(dto.Migrations);
    }

    private static void Validate(ManifestDto dto)
    {
        if (dto.Version != Version)
            throw new MigrationManifestException(
                $"Unsupported manifest version {dto.Version}; expected {Version}.");

        if (dto.Migrations is null || dto.Migrations.Count == 0)
            throw new MigrationManifestException("Manifest must declare at least one migration.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        string previous = string.Empty;

        foreach (var entry in dto.Migrations)
        {
            if (!IsPosition(entry.Id))
                throw new MigrationManifestException(
                    $"Invalid migration id '{entry.Id}'; expected a zero-padded three-digit position.");

            if (!seen.Add(entry.Id))
                throw new MigrationManifestException($"Duplicate migration position '{entry.Id}'.");

            if (string.CompareOrdinal(entry.Id, previous) <= 0)
                throw new MigrationManifestException(
                    $"Migration positions must be strictly ascending; '{entry.Id}' after '{previous}'.");

            previous = entry.Id;

            switch (entry.Phase)
            {
                case PhaseA when string.CompareOrdinal(entry.Id, PhaseAMin) < 0
                                || string.CompareOrdinal(entry.Id, PhaseAMax) > 0:
                    throw new MigrationManifestException(
                        $"Phase A position '{entry.Id}' is outside range {PhaseAMin}-{PhaseAMax}.");

                case PhaseB when string.CompareOrdinal(entry.Id, PhaseBMin) < 0
                                || string.CompareOrdinal(entry.Id, PhaseBMax) > 0:
                    throw new MigrationManifestException(
                        $"Phase B position '{entry.Id}' is outside range {PhaseBMin}-{PhaseBMax}.");

                case not PhaseA when entry.Phase != PhaseB:
                    throw new MigrationManifestException(
                        $"Invalid phase '{entry.Phase}' for position '{entry.Id}'; expected {PhaseA} or {PhaseB}.");
            }

            if (entry.Phase == PhaseA && (entry.Tables is null || entry.Tables.Count == 0))
                throw new MigrationManifestException(
                    $"Phase A position '{entry.Id}' must declare at least one table.");

            if (entry.Phase == PhaseB
                && (entry.Tables is null || entry.Tables.Count == 0)
                && (entry.DeferredConstraints is null || entry.DeferredConstraints.Count == 0))
                throw new MigrationManifestException(
                    $"Phase B position '{entry.Id}' must declare a table or a deferred constraint.");

            if (entry.DeferredConstraints is not null
                && entry.DeferredConstraints.Any(string.IsNullOrWhiteSpace))
                throw new MigrationManifestException(
                    $"Position '{entry.Id}' contains an empty deferred constraint.");
        }
    }

    private static bool IsPosition(string id)
        => id is { Length: 3 } && id.All(char.IsAsciiDigit);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class ManifestDto
    {
        public int Version { get; set; }
        public IReadOnlyList<MigrationManifestEntry> Migrations { get; set; } = [];
    }
}
