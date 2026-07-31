using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Execution;

/// <summary>
/// Integration tests against an empty PostgreSQL 18 instance. Every test
/// runs inside its own freshly created database.
/// </summary>
public sealed class MigrationExecutionTests : IAsyncLifetime
{
    private static readonly string[] SinglePosition = ["001"];
    private static readonly string[] TwoPositions = ["001", "002"];
    private static readonly string[] AppliedOrder = ["001", "002", "003"];

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AppliesAllMigrationsInManifestOrderOnEmptyDatabase()
    {
        using var set = TestMigrationSet.Create(
            Script("001", "stores"),
            Script("002", "printers"),
            Script("003", "payments"));

        var result = await MigrationExecutor.ApplyAsync(
            set.Manifest,
            ForwardFiles(set),
            _database.PsqlOptions,
            CancellationToken.None);

        Assert.True(result.AllApplied, string.Join("\n", result.Steps.Select(s => s.StandardError)));
        Assert.Equal(AppliedOrder, result.AppliedIds);
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.True(await _database.TableExistsAsync("printers"));
        Assert.True(await _database.TableExistsAsync("payments"));
    }

    [Fact]
    public async Task FailsFastAndReportsPartialSuccessWithoutHidingIt()
    {
        using var set = TestMigrationSet.Create(
            Script("001", "stores"),
            Script("002", "payments", upSql: "INSERT INTO nonexistent_payments VALUES (1);"),
            Script("003", "printers"));

        var result = await MigrationExecutor.ApplyAsync(
            set.Manifest,
            ForwardFiles(set),
            _database.PsqlOptions,
            CancellationToken.None);

        Assert.False(result.AllApplied);
        Assert.Equal(SinglePosition, result.AppliedIds);
        Assert.NotNull(result.FailedStep);
        Assert.Equal("002", result.FailedStep.Id);
        Assert.False(result.FailedStep.Success);
        Assert.NotEmpty(result.FailedStep.StandardError);
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.False(await _database.TableExistsAsync("payments"));
        Assert.False(await _database.TableExistsAsync("printers"));
    }

    [Fact]
    public async Task RollbackReversesTheAppliedMigration()
    {
        using var set = TestMigrationSet.Create(
            Script("001", "stores"),
            Script("002", "payments"),
            Script("003", "printers"));

        await MigrationExecutor.ApplyAsync(
            set.Manifest, ForwardFiles(set), _database.PsqlOptions, CancellationToken.None);
        var discovery = MigrationDiscoverer.Discover(set.DirectoryPath);
        var rollbackFile = discovery.Files.Single(f => f.Kind == MigrationScriptKind.Down && f.Id == "002");

        var step = await MigrationExecutor.RollbackAsync(
            "002", rollbackFile, _database.PsqlOptions, CancellationToken.None);

        Assert.True(step.Success, step.StandardError);
        Assert.False(await _database.TableExistsAsync("payments"));
        Assert.True(await _database.TableExistsAsync("stores"));
        Assert.True(await _database.TableExistsAsync("printers"));
    }

    [Fact]
    public async Task DuplicatePositionPreventsAnyExecution()
    {
        using var set = TestMigrationSet.Create(
            Script("001", "stores", downSql: null),
            Script("001", "users", upSql: "CREATE TABLE users (id integer PRIMARY KEY);", downSql: null));

        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            TextWriter.Null);

        Assert.Equal(HostExitCode.StartupFailed, exitCode);
        Assert.False(await _database.TableExistsAsync("stores"));
        Assert.False(await _database.TableExistsAsync("users"));
    }

    [Fact]
    public async Task MissingMigrationPreventsAnyExecution()
    {
        using var set = TestMigrationSet.Create(
            TwoPositions,
            Script("001", "stores"));

        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            TextWriter.Null);

        Assert.Equal(HostExitCode.StartupFailed, exitCode);
        Assert.False(await _database.TableExistsAsync("stores"));
    }

    [Fact]
    public async Task OrderViolationPreventsAnyExecution()
    {
        using var set = TestMigrationSet.Create(
            SinglePosition,
            Script("001", "stores"),
            Script("007", "unauthorized"));

        var exitCode = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions),
            TextWriter.Null);

        Assert.Equal(HostExitCode.StartupFailed, exitCode);
        Assert.False(await _database.TableExistsAsync("stores"));
        Assert.False(await _database.TableExistsAsync("unauthorized"));
    }

    [Fact]
    public async Task RollbackOfUndeclaredOrMissingPositionIsRefused()
    {
        using var set = TestMigrationSet.CreateWithFiles(
            SinglePosition,
            ("001-stores.up.sql", TestMigrationSet.DefaultUpSql("stores")));

        var undeclared = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions, "099"),
            TextWriter.Null);
        var withoutScript = HostComposition.Run(
            new HostCompositionOptions(set.ManifestPath, set.DirectoryPath, _database.PsqlOptions, "001"),
            TextWriter.Null);

        Assert.Equal(HostExitCode.StartupFailed, undeclared);
        Assert.Equal(HostExitCode.StartupFailed, withoutScript);
        Assert.False(await _database.TableExistsAsync("stores"));
    }

    [Fact]
    public void ProgramMainProducesTheDocumentedExitCodes()
    {
        using var success = TestMigrationSet.Create(Script("001", "stores"));
        using var failing = TestMigrationSet.Create(
            Script("001", "stores"),
            Script("002", "payments", upSql: "INSERT INTO nonexistent_payments VALUES (1);"));
        using var violating = TestMigrationSet.CreateWithFiles(
            SinglePosition,
            ("099-rogue.up.sql", "CREATE TABLE rogue (id integer);"));

        string[] WithPassword(string[] arguments)
        {
            var password = _database.PsqlOptions.Password;
            return password is null ? arguments : [.. arguments, "--db-password", password];
        }

        Assert.Equal((int)HostExitCode.Success, Program.Main(WithPassword(
            ["--order-manifest", success.ManifestPath, "--migrations-dir", success.DirectoryPath,
             "--db-url", _database.Url])));
        Assert.Equal((int)HostExitCode.MigrationFailed, Program.Main(WithPassword(
            ["--order-manifest", failing.ManifestPath, "--migrations-dir", failing.DirectoryPath,
             "--db-url", _database.Url])));
        Assert.Equal((int)HostExitCode.StartupFailed, Program.Main(WithPassword(
            ["--order-manifest", violating.ManifestPath, "--migrations-dir", violating.DirectoryPath,
             "--db-url", _database.Url])));
        Assert.Equal((int)HostExitCode.StartupFailed, Program.Main(WithPassword(["--db-url", _database.Url])));
    }

    [Fact]
    public void ProgramMainRejectsDuplicateArguments()
    {
        using var set = TestMigrationSet.Create(Script("001", "stores"));

        var exitCode = Program.Main(
            ["--order-manifest", set.ManifestPath, "--migrations-dir", set.DirectoryPath,
             "--db-url", _database.Url, "--db-url", _database.Url]);

        Assert.Equal((int)HostExitCode.StartupFailed, exitCode);
    }

    private static TestMigrationScript Script(string id, string name, string? upSql = null, string? downSql = null)
        => TestMigrationScript.For(id, TestMigrationSet.PhaseOf(id), name,
            upSql ?? TestMigrationSet.DefaultUpSql(name),
            downSql ?? TestMigrationSet.DefaultDownSql(name));

    private static Dictionary<string, MigrationFile> ForwardFiles(TestMigrationSet set)
    {
        var discovery = MigrationDiscoverer.Discover(set.DirectoryPath);
        return discovery.Files
            .Where(f => f.Kind == MigrationScriptKind.Up)
            .ToDictionary(f => f.Id, StringComparer.Ordinal);
    }
}
