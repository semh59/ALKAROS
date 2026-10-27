using System.Text.Json.Nodes;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Migrations;

public sealed class MigrationFileLineEndingTests
{
    // These scripts were committed with Windows line endings before the policy existed. Their deployed checksums
    // are the SHA-256 of the raw bytes, so rewriting them would make every existing database refuse to start.
    private static readonly string[] LegacyWindowsScripts =
    [
        "V1/V1-KIT-001/014-kitchen-tickets.down.sql",
        "V1/V1-KIT-001/014-kitchen-tickets.up.sql",
        "V1/V1-OPS-001/015-audit-log.down.sql",
        "V1/V1-OPS-001/015-audit-log.up.sql",
        "V11/V11-MNU-001/067-daily-menus.down.sql",
        "V11/V11-MNU-001/067-daily-menus.up.sql",
        "V11/V11-MNU-003/066-static-menu.down.sql",
        "V11/V11-MNU-003/066-static-menu.up.sql",
        "V11/V11-PUR-001/069-purchase-orders-receipts.down.sql",
        "V11/V11-PUR-001/069-purchase-orders-receipts.up.sql",
        "V11/V11-PUR-002/068-suppliers.down.sql",
        "V11/V11-PUR-002/068-suppliers.up.sql",
        "V11/V11-RCP-002/070-recipe-cost-snapshots.down.sql",
        "V11/V11-RCP-002/070-recipe-cost-snapshots.up.sql",
    ];

    private static readonly string Root = MigrationTestPaths.FindRepositoryRoot();

    [Fact]
    public void EveryMigrationScriptUsesUnixLineEndingsExceptTheFrozenLegacyOnes()
    {
        var migrations = Path.Combine(Root, "database", "migrations");
        var offenders = Directory.EnumerateFiles(migrations, "*.sql", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(migrations, path).Replace('\\', '/'))
            .Where(relative => !LegacyWindowsScripts.Contains(relative, StringComparer.Ordinal))
            .Where(relative => File.ReadAllBytes(Path.Combine(migrations, relative)).Contains((byte)'\r'))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheFrozenLegacyListHasNoStaleEntries()
    {
        var migrations = Path.Combine(Root, "database", "migrations");

        Assert.All(LegacyWindowsScripts, relative =>
            Assert.Contains((byte)'\r', File.ReadAllBytes(Path.Combine(migrations, relative))));
    }

    [Fact]
    public void GitNeverRewritesTheLineEndingsOfAMigrationScript()
    {
        var rules = File.ReadAllLines(Path.Combine(Root, ".gitattributes")).Select(line => line.Trim()).ToList();

        Assert.Contains("database/migrations/** -text", rules);
    }
}

[Collection("Host database password environment")]
public sealed class MigrationSchemaTests : IAsyncLifetime
{
    private const string RowVersionType =
        """
        SELECT data_type FROM information_schema.columns
        WHERE table_schema = 'customer_display' AND table_name = 'screensaver_images' AND column_name = 'row_version';
        """;

    private static readonly string Root = MigrationTestPaths.FindRepositoryRoot();
    private readonly TestDatabase _database = new();
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"alkaros-rmd698-{Guid.NewGuid():N}");

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public async Task TheScreensaverTableRefusesUnsupportedTypesEmptyAndOversizedContent()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());

        await AssertCheckViolationAsync(source, "text/plain", new byte[10]);
        await AssertCheckViolationAsync(source, "image/png", []);
        await AssertCheckViolationAsync(source, "video/mp4", new byte[20 * 1024 * 1024 + 1]);
        await InsertScreensaverAsync(source, "video/mp4", new byte[20 * 1024 * 1024]);

        Assert.Equal("bigint", await ScalarAsync<string>(source, RowVersionType));
    }

    [Fact]
    public async Task APairingCodeOrSessionCannotExpireBeforeItIsCreated()
    {
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        await source.CreateCommand(
            "INSERT INTO customer_display.terminals (terminal_id, row_version, created_at, updated_at) " +
            "VALUES ('00000000-0000-0000-0000-000000000001', 1, now(), now());").ExecuteNonQueryAsync();

        var pairing = await Assert.ThrowsAsync<PostgresException>(() => source.CreateCommand(
            """
            INSERT INTO customer_display.pairing_requests
                (request_id, display_id, pairing_secret_hash, code_hash, expires_at, created_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), repeat('a', 64), repeat('b', 64), now(), now());
            """).ExecuteNonQueryAsync());
        var session = await Assert.ThrowsAsync<PostgresException>(() => source.CreateCommand(
            """
            INSERT INTO customer_display.display_sessions
                (session_id, display_id, terminal_id, token_hash, created_at, expires_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), '00000000-0000-0000-0000-000000000001', repeat('c', 64), now(), now());
            """).ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, pairing.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, session.SqlState);
    }

    [Fact]
    public async Task TheLookupIndexesExistAndRollingBackTheMigrationRemovesEverythingItAdded()
    {
        var (order, migrations) = CopyUpTo("201");
        Migrate(order, migrations);
        await using var source = NpgsqlDataSource.Create(ConnectionString());

        Assert.Equal(6, await SchemaObjectCountAsync(source));
        var terminals = await IndexDefinitionAsync(source, "ix_terminals_active_order");
        Assert.Contains("(active_order_id)", terminals, StringComparison.Ordinal);
        Assert.Contains("WHERE (active_order_id IS NOT NULL)", terminals, StringComparison.Ordinal);
        Assert.Contains(
            "(source_reference_id, source_reference_type)",
            await IndexDefinitionAsync(source, "ix_account_transactions_source_reference"),
            StringComparison.Ordinal);

        Rollback(order, migrations, "201");
        Assert.Equal(0, await SchemaObjectCountAsync(source));
        Assert.Equal("integer", await ScalarAsync<string>(source, RowVersionType));

        Migrate(order, migrations);
        Assert.Equal(6, await SchemaObjectCountAsync(source));
    }

    [Fact]
    public async Task TheTerminalKeyColumnExistsAndRollingBackTheMigrationRemovesIt()
    {
        const string columnCount = "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'customer_display' AND table_name = 'terminals' AND column_name = 'terminal_key_hash';";
        const string constraintCount = "SELECT count(*) FROM pg_constraint WHERE conname = 'ck_customer_display_terminal_key_hash';";
        Migrate(OrderPath(), MigrationsPath());
        await using var source = NpgsqlDataSource.Create(ConnectionString());
        Assert.Equal(1L, await ScalarAsync<long>(source, columnCount));
        Assert.Equal(1L, await ScalarAsync<long>(source, constraintCount));

        // A migration can only be rolled back once every later one is, so unwind from the newest down to 202.
        foreach (var id in new[] { "207", "206", "205", "204", "203", "202" })
            Rollback(OrderPath(), MigrationsPath(), id);
        Assert.Equal(0L, await ScalarAsync<long>(source, columnCount));
        Assert.Equal(0L, await ScalarAsync<long>(source, constraintCount));

        Migrate(OrderPath(), MigrationsPath());
        Assert.Equal(1L, await ScalarAsync<long>(source, columnCount));
    }

    [Theory]
    [InlineData("096")]
    [InlineData("100")]
    [InlineData("101")]
    [InlineData("119")]
    public void AMigrationWithoutItsOwnEvidenceRollsBackAndAppliesAgain(string id)
    {
        var (order, migrations) = CopyUpTo(id);

        Migrate(order, migrations);
        Rollback(order, migrations, id);
        Migrate(order, migrations);
    }

    private (string Order, string Migrations) CopyUpTo(string lastId)
    {
        var migrations = Path.Combine(_scratch, "migrations");
        foreach (var file in Directory.EnumerateFiles(MigrationsPath(), "*.sql", SearchOption.AllDirectories))
        {
            if (string.CompareOrdinal(Path.GetFileName(file)[..3], lastId) > 0)
                continue;
            var target = Path.Combine(migrations, Path.GetRelativePath(MigrationsPath(), file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        var manifest = JsonNode.Parse(File.ReadAllText(OrderPath()))!;
        var kept = manifest["migrations"]!.AsArray()
            .Where(entry => string.CompareOrdinal(entry!["id"]!.GetValue<string>(), lastId) <= 0)
            .Select(entry => entry!.DeepClone())
            .ToArray();
        manifest["migrations"] = new JsonArray(kept);
        var order = Path.Combine(_scratch, "order.json");
        File.WriteAllText(order, manifest.ToJsonString());
        return (order, migrations);
    }

    private void Migrate(string order, string migrations) => Run(order, migrations, null);

    private void Rollback(string order, string migrations, string id) => Run(order, migrations, id);

    private void Run(string order, string migrations, string? rollbackId)
    {
        using var output = new StringWriter();
        var exit = HostComposition.Run(
            new HostCompositionOptions(order, migrations, _database.PsqlOptions, rollbackId), output);
        Assert.True(exit == HostExitCode.Success, output.ToString());
    }

    private static string OrderPath() => Path.Combine(Root, "database", "MigrationComposition", "order.json");

    private static string MigrationsPath() => Path.Combine(Root, "database", "migrations");

    private string ConnectionString()
    {
        var uri = new Uri(_database.Url);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString;
    }

    private static async Task InsertScreensaverAsync(NpgsqlDataSource source, string contentType, byte[] content)
    {
        await using var command = source.CreateCommand(
            "INSERT INTO customer_display.screensaver_images (id, content, content_type, updated_at, row_version) " +
            "VALUES (1, @content, @type, now(), 1) ON CONFLICT (id) DO NOTHING;");
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("type", contentType);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertCheckViolationAsync(NpgsqlDataSource source, string contentType, byte[] content)
    {
        var failure = await Assert.ThrowsAsync<PostgresException>(() => InsertScreensaverAsync(source, contentType, content));
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private static Task<string> IndexDefinitionAsync(NpgsqlDataSource source, string name) =>
        ScalarAsync<string>(source, $"SELECT indexdef FROM pg_indexes WHERE indexname = '{name}';");

    // The objects migration 201 adds: two indexes and four constraints; the column type change is checked on its own.
    private static async Task<long> SchemaObjectCountAsync(NpgsqlDataSource source) =>
        await ScalarAsync<long>(
            source,
            """
            SELECT (SELECT count(*) FROM pg_indexes
                    WHERE indexname IN ('ix_terminals_active_order', 'ix_account_transactions_source_reference'))
                 + (SELECT count(*) FROM pg_constraint
                    WHERE conname IN ('pairing_requests_expiry_check', 'display_sessions_expiry_check',
                                      'screensaver_images_content_type_check', 'screensaver_images_size_check'));
            """);
}

internal static class MigrationTestPaths
{
    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
