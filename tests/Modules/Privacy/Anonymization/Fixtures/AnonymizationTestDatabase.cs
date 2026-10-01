using System.Text.Json;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Privacy.Anonymization.Tests.Fixtures;

/// <summary>
/// Every migration in database/MigrationComposition/order.json (the workflow's own tables and the retention work items are
/// real) plus a scratch table the test plans write. Records are seeded with foreign-key triggers off (<c>session_replication_role = replica</c>, local
/// to the seeding transaction); CHECK constraints still apply.
/// </summary>
public sealed class AnonymizationTestDatabase : PgTestDatabase
{
    public AnonymizationTestDatabase()
        : base("alkaros_kvk002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString()!;
            var file = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }

        await RunAsync(DataSource, "CREATE TABLE public.anon_scratch (id uuid PRIMARY KEY, a text NULL, b text NULL, blocked boolean NOT NULL DEFAULT false);");
    }

    /// <summary>A record with two personal fields in two stores (columns a and b of the scratch table).</summary>
    public async Task<Guid> SeedRecordAsync(bool blocked = false)
    {
        var id = Guid.NewGuid();
        await SeedAsync(
            "INSERT INTO public.anon_scratch (id, a, b, blocked) VALUES (@id, 'Ahmet Bey', '05001112233', @blocked);",
            ("id", id), ("blocked", blocked));
        return id;
    }

    /// <summary>Queues the records as pending work items the way a retention run does.</summary>
    public Task QueueAsync(string dataClass, Guid subject)
        => SeedAsync(
            """
            INSERT INTO privacy.retention_runs (run_id, policy_version, as_of, requested_by, item_count)
            VALUES (@run, 1, now(), 'test', 1);
            INSERT INTO privacy.retention_work_items (data_class, subject_id, run_id, policy_version, due_since)
            VALUES (@class, @subject, @run, 1, now());
            """,
            ("run", Guid.NewGuid()), ("class", dataClass), ("subject", subject));

    public Task SeedAsync(string sql, params (string Name, object Value)[] parameters) => WriteAsync(sql, true, parameters);

    public Task RunSqlAsync(string sql, params (string Name, object Value)[] parameters) => WriteAsync(sql, false, parameters);

    private async Task WriteAsync(string sql, bool withoutForeignKeys, (string Name, object Value)[] parameters)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        if (withoutForeignKeys)
            await using (var replica = new NpgsqlCommand("SET LOCAL session_replication_role = replica;", connection, transaction))
                await replica.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (ALKAROS.slnx) not found.");
    }
}
