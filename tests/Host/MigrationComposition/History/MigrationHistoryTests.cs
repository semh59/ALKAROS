using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.History;

[Collection("Host database password environment")]
public sealed class MigrationHistoryTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task FailedScriptLeavesNoProductSchemaOrHistoryEntry()
    {
        using var set = TestMigrationSet.Create(Script(
            "001",
            "atomic_target",
            "CREATE TABLE atomic_target (id integer PRIMARY KEY); INSERT INTO missing_target VALUES (1);"));

        var exitCode = RunHost(set);

        Assert.Equal(HostExitCode.MigrationFailed, exitCode);
        Assert.False(await _database.TableExistsAsync("atomic_target"));
        Assert.Equal(0, await HistoryCountAsync());
    }

    [Fact]
    public async Task MatchingChecksumIsSkippedOnReRun()
    {
        using var set = TestMigrationSet.Create(Script("001", "stores"));

        Assert.Equal(HostExitCode.Success, RunHost(set));
        using var output = new StringWriter();
        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            output);

        Assert.Equal(HostExitCode.Success, exitCode);
        Assert.Contains("1 migration(s) verified; 0 applied", output.ToString(), StringComparison.Ordinal);
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.Equal(1, await HistoryCountAsync());
    }

    [Fact]
    public async Task ChangedChecksumIsRejectedBeforeScriptExecution()
    {
        using var set = TestMigrationSet.Create(Script("001", "stores"));
        Assert.Equal(HostExitCode.Success, RunHost(set));

        var forwardScript = MigrationDiscoverer.Discover(set.DirectoryPath).Files
            .Single(file => file.Kind == MigrationScriptKind.Up);
        await File.WriteAllTextAsync(
            forwardScript.Path,
            "CREATE TABLE changed_schema_must_not_run (id integer PRIMARY KEY);");

        using var output = new StringWriter();
        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            output);

        Assert.Equal(HostExitCode.MigrationFailed, exitCode);
        Assert.Contains("checksum differs", output.ToString(), StringComparison.Ordinal);
        Assert.False(await _database.TableExistsAsync("changed_schema_must_not_run"));
        Assert.Equal(1, await HistoryCountAsync());
    }

    [Fact]
    public async Task RollbackIsRefusedWhenPositionIsUnappliedOrHasLaterHistory()
    {
        using var set = TestMigrationSet.Create(Script("001", "stores"), Script("002", "payments"));

        var unapplied = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions, "001"),
            TextWriter.Null);
        Assert.Equal(HostExitCode.StartupFailed, unapplied);

        Assert.Equal(HostExitCode.Success, RunHost(set));
        var withLaterPosition = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions, "001"),
            TextWriter.Null);

        Assert.Equal(HostExitCode.StartupFailed, withLaterPosition);
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.True(await _database.TableExistsAsync("payments"));
        Assert.Equal(2, await HistoryCountAsync());
    }

    [Fact]
    public async Task FailedRollbackLeavesSchemaAndHistoryEntry()
    {
        using var set = TestMigrationSet.Create(Script(
            "001",
            "stores",
            downSql: "DROP TABLE stores; INSERT INTO missing_rollback_target VALUES (1);"));
        Assert.Equal(HostExitCode.Success, RunHost(set));

        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions, "001"),
            TextWriter.Null);

        Assert.Equal(HostExitCode.MigrationFailed, exitCode);
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.Equal(1, await HistoryCountAsync());
    }

    [Fact]
    public async Task SuccessfulRollbackRemovesSchemaAndHistoryEntryTogether()
    {
        using var set = TestMigrationSet.Create(Script("001", "stores"));
        Assert.Equal(HostExitCode.Success, RunHost(set));

        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions, "001"),
            TextWriter.Null);

        Assert.Equal(HostExitCode.Success, exitCode);
        Assert.False(await _database.TableExistsAsync("stores"));
        Assert.Equal(0, await HistoryCountAsync());
    }

    [Fact]
    public async Task MismatchedHistoryTableSchemaIsRejectedBeforeAnyWrite()
    {
        var createMismatch = await PsqlScriptRunner.RunCommandAsync(
            "CREATE TABLE alkaros_schema_migrations (migration_id text PRIMARY KEY, checksum text NOT NULL);",
            _database.PsqlOptions,
            CancellationToken.None);
        Assert.True(createMismatch.Success, createMismatch.ErrorSummary);

        using var set = TestMigrationSet.Create(Script("001", "stores"));
        using var output = new StringWriter();
        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            output);

        Assert.Equal(HostExitCode.MigrationFailed, exitCode);
        Assert.Contains("does not match the expected contract", output.ToString(), StringComparison.Ordinal);
        Assert.False(await _database.TableExistsAsync("stores"));
        Assert.Equal(0, await HistoryCountAsync());
    }

    [Fact]
    public async Task MatchingHistoryTableSchemaIsAccepted()
    {
        var createMatch = await PsqlScriptRunner.RunCommandAsync(
            """
            CREATE TABLE alkaros_schema_migrations (
                migration_id text PRIMARY KEY CHECK (migration_id ~ '^[0-9]{3}$'),
                checksum text NOT NULL CHECK (checksum ~ '^[0-9A-F]{64}$'),
                applied_at timestamp with time zone NOT NULL DEFAULT now()
            );
            """,
            _database.PsqlOptions,
            CancellationToken.None);
        Assert.True(createMatch.Success, createMatch.ErrorSummary);

        using var set = TestMigrationSet.Create(Script("001", "stores"));
        using var output = new StringWriter();
        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            output);

        Assert.Equal(HostExitCode.Success, exitCode);
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.Equal(1, await HistoryCountAsync());
    }

    private HostExitCode RunHost(TestMigrationSet set)
        => HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            TextWriter.Null);

    private async Task<int> HistoryCountAsync()
    {
        var result = await PsqlScriptRunner.RunCommandAsync(
            $"SELECT count(*) FROM {MigrationHistoryStore.TableName};",
            _database.PsqlOptions,
            CancellationToken.None);
        Assert.True(result.Success, result.ErrorSummary);
        return int.Parse(result.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static TestMigrationScript Script(
        string id,
        string name,
        string? upSql = null,
        string? downSql = null)
        => TestMigrationScript.For(
            id,
            TestMigrationSet.PhaseOf(id),
            name,
            upSql ?? TestMigrationSet.DefaultUpSql(name),
            downSql ?? TestMigrationSet.DefaultDownSql(name));
}
