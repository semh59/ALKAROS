using System.Diagnostics;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

// The runtime roles of deploy/docker/roles.sql, exercised on a database the real migrations built.
[Collection("Host database password environment")]
public sealed class DatabaseRolesTests : IAsyncLifetime
{
    private const string AppPassword = "app-pw-for-tests";
    private const string RestorePassword = "restore-pw-for-tests";

    private static readonly string Root = FindRepositoryRoot();
    private readonly TestDatabase _database = new();
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

    private string AppRole => $"rmd700_app_{_suffix}";

    private string RestoreRole => $"rmd700_restore_{_suffix}";

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        using var output = new StringWriter();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(Root, "database", "MigrationComposition", "order.json"),
                Path.Combine(Root, "database", "migrations"),
                _database.PsqlOptions),
            output);
        Assert.True(exit == HostExitCode.Success, output.ToString());
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        foreach (var role in new[] { AppRole, RestoreRole })
            await PsqlScriptRunner.RunCommandAsync($"DROP ROLE IF EXISTS {role};", _database.MaintenanceOptions, CancellationToken.None);
    }

    [Fact]
    public async Task TheRuntimeRolesHaveOnlyTheirOwnPrivilegesAndTheRestoreRoleHasNoDataAccess()
    {
        ApplyRoles();

        Assert.Equal(
            (false, false, false, false, false),
            await RoleFlagsAsync(AppRole));
        Assert.Equal(
            (false, true, false, false, false),
            await RoleFlagsAsync(RestoreRole));

        await using var restore = ConnectAs(RestoreRole, RestorePassword);
        var scratch = $"rmd700_scratch_{_suffix}";
        await ExecuteAsync(restore, $"CREATE DATABASE {scratch};");
        await ExecuteAsync(restore, $"DROP DATABASE {scratch};");
        await AssertDeniedAsync(restore, "SELECT count(*) FROM identity.permissions;");
    }

    [Fact]
    public async Task TheApplicationRoleReadsAndWritesDataButCannotChangeTheSchemaOrTheMigrationHistory()
    {
        ApplyRoles();
        await using var app = ConnectAs(AppRole, AppPassword);

        await ExecuteAsync(app, "INSERT INTO identity.permissions (permission_id, code, name) VALUES (gen_random_uuid(), 'rmd700.probe', 'Probe');");
        await ExecuteAsync(app, "UPDATE identity.permissions SET name = 'Probe 2' WHERE code = 'rmd700.probe';");
        await ExecuteAsync(app, "DELETE FROM identity.permissions WHERE code = 'rmd700.probe';");

        foreach (var forbidden in new[]
        {
            "CREATE DATABASE rmd700_nope;",
            $"CREATE ROLE rmd700_nope_{_suffix};",
            "CREATE TABLE public.rmd700_nope (id int);",
            "CREATE SCHEMA rmd700_nope;",
            "ALTER TABLE identity.permissions ADD COLUMN rmd700_nope int;",
            "DROP TABLE identity.permissions;",
            "TRUNCATE identity.permissions;",
            "SELECT count(*) FROM public.alkaros_schema_migrations;",
            "DELETE FROM public.alkaros_schema_migrations;",
        })
        {
            await AssertDeniedAsync(app, forbidden);
        }
    }

    [Fact]
    public async Task ATableAddedLaterIsGrantedWhenTheScriptRunsAgainAndRunningItTwiceChangesNothing()
    {
        ApplyRoles();
        await using (var owner = NpgsqlDataSource.Create(OwnerConnection()))
        {
            await owner.CreateCommand("CREATE SCHEMA rmd700_late; CREATE TABLE rmd700_late.items (id int primary key);").ExecuteNonQueryAsync();
        }

        await using var app = ConnectAs(AppRole, AppPassword);
        await AssertDeniedAsync(app, "INSERT INTO rmd700_late.items VALUES (1);");

        ApplyRoles();
        ApplyRoles();

        await using var again = ConnectAs(AppRole, AppPassword);
        await ExecuteAsync(again, "INSERT INTO rmd700_late.items VALUES (1);");
    }

    [Fact]
    public void WithoutThePasswordsInTheEnvironmentTheScriptRefusesToRun()
    {
        var result = RunRolesScript(withPasswords: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("APP_DB_PASSWORD is not set", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePasswordsAreStoredHashedAndEveryRunRewritesThem()
    {
        ApplyRoles();
        var first = await StoredPasswordAsync(AppRole);
        ApplyRoles();
        var second = await StoredPasswordAsync(AppRole);

        Assert.StartsWith("SCRAM-SHA-256$", first, StringComparison.Ordinal);
        Assert.DoesNotContain(AppPassword, first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    private void ApplyRoles()
    {
        var result = RunRolesScript();
        Assert.True(result.ExitCode == 0, result.Error);
    }

    private (int ExitCode, string Error) RunRolesScript(bool withPasswords = true)
    {
        var start = PsqlScriptRunner.CreateStartInfo(_database.PsqlOptions);
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add($"app_role={AppRole}");
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add($"restore_role={RestoreRole}");
        start.ArgumentList.Add("--file");
        start.ArgumentList.Add(Path.Combine(Root, "deploy", "docker", "roles.sql"));
        start.Environment.Remove("APP_DB_PASSWORD");
        start.Environment.Remove("RESTORE_DB_PASSWORD");
        if (withPasswords)
        {
            start.Environment["APP_DB_PASSWORD"] = AppPassword;
            start.Environment["RESTORE_DB_PASSWORD"] = RestorePassword;
        }

        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, error);
    }

    private string OwnerConnection() => ConnectionString(null, null);

    private NpgsqlDataSource ConnectAs(string role, string password) => NpgsqlDataSource.Create(ConnectionString(role, password));

    private string ConnectionString(string? role, string? password)
    {
        var uri = new Uri(_database.Url);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = role ?? uri.UserInfo,
            Password = password ?? _database.PsqlOptions.Password,
            Pooling = false,
        }.ConnectionString;
    }

    private async Task<string> StoredPasswordAsync(string role)
    {
        await using var owner = NpgsqlDataSource.Create(OwnerConnection());
        await using var command = owner.CreateCommand("SELECT rolpassword FROM pg_authid WHERE rolname = @name;");
        command.Parameters.AddWithValue("name", role);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<(bool Super, bool CreateDb, bool CreateRole, bool Replication, bool BypassRls)> RoleFlagsAsync(string role)
    {
        await using var owner = NpgsqlDataSource.Create(OwnerConnection());
        await using var command = owner.CreateCommand(
            "SELECT rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls FROM pg_roles WHERE rolname = @name;");
        command.Parameters.AddWithValue("name", role);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4));
    }

    private static async Task ExecuteAsync(NpgsqlDataSource source, string sql)
    {
        await using var command = source.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertDeniedAsync(NpgsqlDataSource source, string sql)
    {
        var failure = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(source, sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
