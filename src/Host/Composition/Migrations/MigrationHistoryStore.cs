namespace ALKAROS.Host.Composition.Migrations;

public sealed record AppliedMigration(string Id, string Checksum);

public sealed record MigrationHistoryReadResult(
    bool Success,
    IReadOnlyDictionary<string, AppliedMigration> Entries,
    string StandardError);

/// <summary>
/// Persists the immutable checksum of each migration that committed. The
/// control table deliberately contains no product schema and is created
/// before the first migration is attempted.
/// </summary>
public static class MigrationHistoryStore
{
    public const string TableName = "alkaros_schema_migrations";

    private const string CreateTableCommand = """
        CREATE TABLE IF NOT EXISTS alkaros_schema_migrations (
            migration_id text PRIMARY KEY CHECK (migration_id ~ '^[0-9]{3}$'),
            checksum text NOT NULL CHECK (checksum ~ '^[0-9A-F]{64}$'),
            applied_at timestamp with time zone NOT NULL DEFAULT now()
        );
        """;

    public static Task<ScriptExecutionResult> EnsureAsync(
        PsqlOptions options,
        CancellationToken cancellationToken)
        => PsqlScriptRunner.RunCommandAsync(CreateTableCommand, options, cancellationToken);

    public static async Task<MigrationHistoryReadResult> ReadAsync(
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var execution = await PsqlScriptRunner.RunCommandAsync(
            "SELECT migration_id || E'\\t' || checksum FROM alkaros_schema_migrations ORDER BY migration_id;",
            options,
            cancellationToken).ConfigureAwait(false);
        if (!execution.Success)
            return new MigrationHistoryReadResult(false, EmptyEntries, execution.StandardError);

        var entries = new Dictionary<string, AppliedMigration>(StringComparer.Ordinal);
        foreach (var line in execution.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts is not [var id, var checksum]
                || !IsMigrationId(id)
                || !IsChecksum(checksum)
                || !entries.TryAdd(id, new AppliedMigration(id, checksum)))
            {
                return new MigrationHistoryReadResult(
                    false,
                    EmptyEntries,
                    "Migration history contains an invalid or duplicate entry.");
            }
        }

        return new MigrationHistoryReadResult(true, entries, string.Empty);
    }

    public static string BuildInsertCommand(string migrationId, string checksum)
    {
        ValidateValues(migrationId, checksum);
        return $"INSERT INTO {TableName} (migration_id, checksum) VALUES ('{migrationId}', '{checksum}');";
    }

    public static string BuildDeleteCommand(string migrationId)
    {
        if (!IsMigrationId(migrationId))
            throw new ArgumentException("Migration id must be a zero-padded three-digit value.", nameof(migrationId));

        return $"DELETE FROM {TableName} WHERE migration_id = '{migrationId}';";
    }

    private static void ValidateValues(string migrationId, string checksum)
    {
        if (!IsMigrationId(migrationId))
            throw new ArgumentException("Migration id must be a zero-padded three-digit value.", nameof(migrationId));
        if (!IsChecksum(checksum))
            throw new ArgumentException("Migration checksum must be an uppercase SHA-256 value.", nameof(checksum));
    }

    private static bool IsMigrationId(string value)
        => value.Length == 3 && value.All(char.IsAsciiDigit);

    private static bool IsChecksum(string value)
        => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static readonly IReadOnlyDictionary<string, AppliedMigration> EmptyEntries
        = new Dictionary<string, AppliedMigration>(StringComparer.Ordinal);
}
