using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

/// <summary>
/// Verifies the PostgreSQL 18 extension lifecycle and rollback policy defined by
/// V0-DAT-007 and implemented by V1-FND-021:
/// - Dedicated migration 012-btree-gist-ownership executes CREATE EXTENSION IF NOT EXISTS btree_gist;
/// - Rollback executes DROP EXTENSION IF EXISTS btree_gist;
/// - Verifies fresh DB, pre-existing extension, and forward-down-forward symmetry.
/// </summary>
[Collection("Host database password environment")]
public sealed class PostgresqlExtensionLifecycleTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private async Task<bool> ExtensionExistsAsync(string extensionName)
    {
        var result = await PsqlScriptRunner.RunCommandAsync(
            $"SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = '{extensionName}');",
            _database.PsqlOptions,
            CancellationToken.None);

        Assert.True(result.Success, $"Extension check query failed: {result.ErrorSummary}");
        return string.Equals(result.StandardOutput.Trim(), "t", StringComparison.Ordinal);
    }

    private static string GetMigrationPath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
        {
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        var path = Path.Combine(dir, "database", "migrations", "V1", "V1-FND-021", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Migration file not found: {path}");
        return path;
    }

    [Fact]
    public async Task FreshEmptyDatabaseForwardAndRollbackLifecycle()
    {
        // 1. Initial State: Fresh DB, extension must not exist
        Assert.False(await ExtensionExistsAsync("btree_gist"));

        // 2. Forward Migration 012
        var upScriptPath = GetMigrationPath("012-btree-gist-ownership.up.sql");
        var upResult = await PsqlScriptRunner.RunAsync(
            upScriptPath,
            _database.PsqlOptions,
            CancellationToken.None);

        Assert.True(upResult.Success, $"Forward migration failed: {upResult.ErrorSummary}");
        Assert.True(await ExtensionExistsAsync("btree_gist"), "Extension must exist after forward migration");

        // 3. Rollback Migration 012
        var downScriptPath = GetMigrationPath("012-btree-gist-ownership.down.sql");
        var downResult = await PsqlScriptRunner.RunAsync(
            downScriptPath,
            _database.PsqlOptions,
            CancellationToken.None);

        Assert.True(downResult.Success, $"Rollback migration failed: {downResult.ErrorSummary}");
        Assert.False(await ExtensionExistsAsync("btree_gist"), "Extension must be absent after rollback on clean DB");
    }

    [Fact]
    public async Task PreExistingExtensionIsHandledIdempotently()
    {
        // 1. Pre-provision extension
        var preCreate = await PsqlScriptRunner.RunCommandAsync(
            "CREATE EXTENSION IF NOT EXISTS btree_gist;",
            _database.PsqlOptions,
            CancellationToken.None);
        Assert.True(preCreate.Success, $"Pre-creation failed: {preCreate.ErrorSummary}");
        Assert.True(await ExtensionExistsAsync("btree_gist"));

        // 2. Forward Migration 012 must succeed idempotently
        var upScriptPath = GetMigrationPath("012-btree-gist-ownership.up.sql");
        var upResult = await PsqlScriptRunner.RunAsync(
            upScriptPath,
            _database.PsqlOptions,
            CancellationToken.None);

        Assert.True(upResult.Success, $"Idempotent forward migration failed: {upResult.ErrorSummary}");
        Assert.True(await ExtensionExistsAsync("btree_gist"));
    }

    [Fact]
    public async Task ForwardDownForwardSymmetry()
    {
        var upScriptPath = GetMigrationPath("012-btree-gist-ownership.up.sql");
        var downScriptPath = GetMigrationPath("012-btree-gist-ownership.down.sql");

        // Forward
        var up1 = await PsqlScriptRunner.RunAsync(upScriptPath, _database.PsqlOptions, CancellationToken.None);
        Assert.True(up1.Success);
        Assert.True(await ExtensionExistsAsync("btree_gist"));

        // Down
        var down1 = await PsqlScriptRunner.RunAsync(downScriptPath, _database.PsqlOptions, CancellationToken.None);
        Assert.True(down1.Success);
        Assert.False(await ExtensionExistsAsync("btree_gist"));

        // Forward again
        var up2 = await PsqlScriptRunner.RunAsync(upScriptPath, _database.PsqlOptions, CancellationToken.None);
        Assert.True(up2.Success);
        Assert.True(await ExtensionExistsAsync("btree_gist"));
    }
}
