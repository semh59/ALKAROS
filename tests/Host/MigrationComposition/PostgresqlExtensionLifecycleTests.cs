using System.Security.Cryptography;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

[Collection("Host database password environment")]
public sealed class PostgresqlExtensionLifecycleTests : IAsyncLifetime
{
    private const string CatalogUpChecksum = "DD632AB7C02A188374F5B6251388170045E496017D434E207B094BE5B7F0F0E8";
    private const string OwnershipUpChecksum = "22BC242F56E99B116A0360D9C9DCE331097BB8F1BA6FEB22A1DD6E25A8C235CD";
    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public void ForwardMigrationIdentityRemainsCompatibleWithApplied007And012History()
    {
        Assert.Equal(CatalogUpChecksum, Checksum(MigrationPath("V1-CAT-002", "007-catalog-pricing.up.sql")));
        Assert.Equal(OwnershipUpChecksum, Checksum(MigrationPath("V1-FND-021", "012-btree-gist-ownership.up.sql")));
    }

    [Fact]
    public async Task Partial012RollbackKeeps007ConstraintAndFull007RollbackDropsInDependencyOrder()
    {
        await ApplyAsync("V1-CAT-001", "006-catalog.up.sql");
        await ApplyAsync("V1-CAT-002", "007-catalog-pricing.up.sql");
        await ApplyAsync("V1-FND-021", "012-btree-gist-ownership.up.sql");

        await ApplyAsync("V1-FND-021", "012-btree-gist-ownership.down.sql");

        Assert.True(await ExtensionExistsAsync());
        Assert.True(await ProductPricesExistsAsync());
        Assert.True(await ExclusionConstraintExistsAsync());

        await ApplyAsync("V1-CAT-002", "007-catalog-pricing.down.sql");

        Assert.False(await ProductPricesExistsAsync());
        Assert.False(await ExtensionExistsAsync());
    }

    [Fact]
    public async Task PreExistingUnsharedExtensionIsKeptAtPartialRollbackAndRemovedOnlyAtFullRollback()
    {
        await ExecuteAsync("CREATE EXTENSION btree_gist;");
        await ApplyAsync("V1-CAT-001", "006-catalog.up.sql");
        await ApplyAsync("V1-CAT-002", "007-catalog-pricing.up.sql");
        await ApplyAsync("V1-FND-021", "012-btree-gist-ownership.up.sql");

        await ApplyAsync("V1-FND-021", "012-btree-gist-ownership.down.sql");
        Assert.True(await ExtensionExistsAsync());

        await ApplyAsync("V1-CAT-002", "007-catalog-pricing.down.sql");
        Assert.False(await ExtensionExistsAsync());
    }

    [Fact]
    public async Task ExternalExtensionDependencyMakesFullRollbackFailClosedWithoutPartialObjectLoss()
    {
        await ExecuteAsync(
            """
            CREATE EXTENSION btree_gist;
            CREATE SCHEMA external_owner;
            CREATE TABLE external_owner.ranges (
                range_id integer NOT NULL,
                valid_during int4range NOT NULL,
                EXCLUDE USING gist (range_id WITH =, valid_during WITH &&)
            );
            """);
        await ApplyAsync("V1-CAT-001", "006-catalog.up.sql");
        await ApplyAsync("V1-CAT-002", "007-catalog-pricing.up.sql");
        await ApplyAsync("V1-FND-021", "012-btree-gist-ownership.up.sql");
        await ApplyAsync("V1-FND-021", "012-btree-gist-ownership.down.sql");

        var rollback = await RunAsync("V1-CAT-002", "007-catalog-pricing.down.sql");

        Assert.False(rollback.Success);
        Assert.True(await ExtensionExistsAsync());
        Assert.True(await ProductPricesExistsAsync());
        Assert.True(await RelationExistsAsync("external_owner.ranges"));

        await ExecuteAsync("DROP SCHEMA external_owner CASCADE;");
        await ApplyAsync("V1-CAT-002", "007-catalog-pricing.down.sql");
        Assert.False(await ExtensionExistsAsync());
    }

    [Fact]
    public async Task PartialRollbackAssertionRejectsMissing007Dependency()
    {
        await ExecuteAsync("CREATE EXTENSION btree_gist;");

        var rollback = await RunAsync("V1-FND-021", "012-btree-gist-ownership.down.sql");

        Assert.False(rollback.Success);
        Assert.True(await ExtensionExistsAsync());
    }

    private async Task ApplyAsync(string taskDirectory, string fileName)
    {
        var result = await RunAsync(taskDirectory, fileName);
        Assert.True(result.Success, $"{fileName} failed: {result.ErrorSummary}");
    }

    private Task<ScriptExecutionResult> RunAsync(string taskDirectory, string fileName)
        => PsqlScriptRunner.RunAsync(
            MigrationPath(taskDirectory, fileName),
            _database.PsqlOptions,
            CancellationToken.None);

    private async Task ExecuteAsync(string sql)
    {
        var result = await PsqlScriptRunner.RunCommandAsync(sql, _database.PsqlOptions, CancellationToken.None);
        Assert.True(result.Success, result.ErrorSummary);
    }

    private async Task<bool> ExtensionExistsAsync()
        => await ScalarBooleanAsync("SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist');");

    private async Task<bool> ProductPricesExistsAsync()
        => await RelationExistsAsync("catalog.product_prices");

    private async Task<bool> RelationExistsAsync(string relation)
        => await ScalarBooleanAsync($"SELECT to_regclass('{relation}') IS NOT NULL;");

    private async Task<bool> ExclusionConstraintExistsAsync()
        => await ScalarBooleanAsync(
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_constraint
                WHERE conrelid = 'catalog.product_prices'::regclass
                  AND conname = 'excl_product_prices_no_overlap'
            );
            """);

    private async Task<bool> ScalarBooleanAsync(string sql)
    {
        var result = await PsqlScriptRunner.RunCommandAsync(sql, _database.PsqlOptions, CancellationToken.None);
        Assert.True(result.Success, result.ErrorSummary);
        return string.Equals(result.StandardOutput.Trim(), "t", StringComparison.Ordinal);
    }

    private static string MigrationPath(string taskDirectory, string fileName)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "database", "migrations", "V1", taskDirectory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Migration file not found: {path}");
        return path;
    }

    private static string Checksum(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
