using ALKAROS.Audit.PartitionDisposal;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Program;

[Collection("Host database password environment")]
public sealed class AuditDisposalTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();
    private NpgsqlDataSource? _dataSource;
    private string? _originalPassword;

    public async Task InitializeAsync()
    {
        _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
        await _database.InitializeAsync();

        var root = FindRepositoryRoot();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(root, "database", "MigrationComposition", "order.json"),
                Path.Combine(root, "database", "migrations"),
                _database.PsqlOptions),
            TextWriter.Null);
        Assert.Equal(HostExitCode.Success, exit);

        var uri = new Uri(_database.Url);
        _dataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString);
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _database.PsqlOptions.Password);
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task TheLogIsPartitionedByYearStaysAppendOnlyAndCatchesAnyDate()
    {
        await InsertAsync("2026-05-05", "2100-01-01", "1999-01-01");

        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events_y2026;"));
        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events_default;"));
        foreach (var statement in new[]
        {
            "UPDATE audit.audit_events SET reason = 'x';",
            "DELETE FROM audit.audit_events;",
            "UPDATE audit.audit_events_y2026 SET reason = 'x';",
            "DELETE FROM audit.audit_events_y2026;",
            "UPDATE audit.audit_events_default SET reason = 'x';",
        })
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(statement));
            Assert.Contains("append-only", ex.MessageText, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(2015, "2025-12-31T23:59:59Z", false)]
    [InlineData(2015, "2026-01-01T00:00:00Z", true)]
    [InlineData(2016, "2026-10-01T00:00:00Z", false)]
    public void ARetentionYearEndsTenFullYearsAfterTheYearEnds(int year, string now, bool expected)
        => Assert.Equal(expected, AuditPartitionDisposal.IsExpired(year, DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public async Task DryRunChangesNothingAndApplyDropsOnlyExpiredYearsOnceAndKeepsADisposalRecord()
    {
        await CreateYearAsync(2015);
        await CreateYearAsync(2016);
        await InsertAsync("2015-03-03", "2015-07-07", "2016-03-03", "2026-05-05", "1999-01-01");
        var disposal = new AuditPartitionDisposal(_dataSource!);

        var dry = await disposal.RunAsync(Now, apply: false);
        Assert.Empty(dry.Dropped);
        Assert.Equal(["audit_events_y2015"], dry.Partitions.Where(p => p.Expired).Select(p => p.Name));
        Assert.Equal(2L, dry.Partitions.Single(p => p.Year == 2015).Rows);
        Assert.Equal(1L, dry.DefaultPartitionRows);
        Assert.Equal(5L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events;"));

        var applied = await disposal.RunAsync(Now, apply: true, operatorName: "qa.operator");
        Assert.Equal(["audit_events_y2015"], applied.Dropped.Select(p => p.Name));
        Assert.False(await ScalarAsync<bool>("SELECT to_regclass('audit.audit_events_y2015') IS NOT NULL;"));
        Assert.True(await ScalarAsync<bool>("SELECT to_regclass('audit.audit_events_y2016') IS NOT NULL;"));
        Assert.True(await ScalarAsync<bool>("SELECT to_regclass('audit.audit_events_default') IS NOT NULL;"));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events_default WHERE occurred_at < '2000-01-01+00';"));
        Assert.Equal(
            "audit_events_y2015:2",
            await ScalarAsync<string>(
                "SELECT metadata_json->>'partition' || ':' || (metadata_json->>'rows') FROM audit.audit_events WHERE event_name = 'audit.partition.disposed';"));

        var again = await disposal.RunAsync(Now, apply: true, operatorName: "qa.operator");
        Assert.Empty(again.Dropped);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events WHERE event_name = 'audit.partition.disposed';"));
    }

    [Fact]
    public async Task ThePartitionAfterTheLastPreCreatedYearIsNeverDroppedWhileInsideRetention()
    {
        var result = await new AuditPartitionDisposal(_dataSource!).RunAsync(new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero), apply: false);

        Assert.Equal(Enumerable.Range(2020, 41).Select(y => $"audit_events_y{y}"), result.Partitions.Select(p => p.Name));
        Assert.Equal(Enumerable.Range(2020, 10).Select(y => $"audit_events_y{y}"), result.Partitions.Where(p => p.Expired).Select(p => p.Name));
    }

    [Fact]
    public void TheCommandPreviewsWithAnAsOfDateButRefusesToApplyOne()
    {
        var originalOut = Console.Out;
        using var dryOut = new StringWriter();
        int dryExit;
        try
        {
            Console.SetOut(dryOut);
            dryExit = ALKAROS.Host.Program.Main(["audit-disposal", "--db-url", _database.Url, "--as-of", "2040-01-01"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal((int)HostExitCode.Success, dryExit);
        Assert.Contains("expired=10", dryOut.ToString(), StringComparison.Ordinal);
        Assert.Contains("apply=false", dryOut.ToString(), StringComparison.Ordinal);

        var originalErr = Console.Error;
        using var err = new StringWriter();
        int applyExit;
        try
        {
            Console.SetError(err);
            applyExit = ALKAROS.Host.Program.Main(["audit-disposal", "--db-url", _database.Url, "--apply", "--operator", "qa.operator", "--as-of", "2040-01-01"]);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        Assert.NotEqual((int)HostExitCode.Success, applyExit);
        Assert.Contains("birlikte kullanılamaz", err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TableWideClearIsRefusedOnTheParentAndEveryPartition()
    {
        await CreateYearAsync(2015);
        await ExecuteAsync("SELECT audit.guard_audit_partition('audit.audit_events_y2015'::regclass);");
        foreach (var target in new[] { "audit.audit_events", "audit.audit_events_y2015", "audit.audit_events_y2026", "audit.audit_events_default" })
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync($"TRUNCATE {target};"));
            Assert.Contains("append-only", ex.MessageText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EveryExistingPartitionCarriesTheClearGuard()
    {
        Assert.Equal(
            await ScalarAsync<long>("SELECT count(*) + 1 FROM pg_inherits WHERE inhparent = 'audit.audit_events'::regclass;"),
            await ScalarAsync<long>(
                "SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_audit_events_no_bulk_clear' AND NOT tgisinternal;"));
    }

    [Fact]
    public async Task TheClearGuardMigrationRollsBackAndForwardIdempotently()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "database", "migrations", "V1", "V1-RMD-645");
        var up = await File.ReadAllTextAsync(Path.Combine(folder, "194-audit-bulk-clear-guard.up.sql"));
        var down = await File.ReadAllTextAsync(Path.Combine(folder, "194-audit-bulk-clear-guard.down.sql"));

        await ExecuteAsync(up);
        await ExecuteAsync(down);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_audit_events_no_bulk_clear';"));
        await ExecuteAsync(down);
        await ExecuteAsync(up);
        await ExecuteAsync(up);
        Assert.True(await ScalarAsync<long>("SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_audit_events_no_bulk_clear';") > 40);
    }

    [Fact]
    public async Task ApplyNeedsAnOperatorNameAndRecordsItInTheDisposalEvent()
    {
        await CreateYearAsync(2015);
        var disposal = new AuditPartitionDisposal(_dataSource!);

        await Assert.ThrowsAsync<ArgumentException>(() => disposal.RunAsync(Now, apply: true));
        await Assert.ThrowsAsync<ArgumentException>(() => disposal.RunAsync(Now, apply: true, operatorName: "  "));
        Assert.True(await ScalarAsync<bool>("SELECT to_regclass('audit.audit_events_y2015') IS NOT NULL;"));

        await disposal.RunAsync(Now, apply: true, operatorName: "qa.operator");
        Assert.Equal(
            "qa.operator",
            await ScalarAsync<string>("SELECT metadata_json->>'operator' FROM audit.audit_events WHERE event_name = 'audit.partition.disposed';"));
    }

    [Fact]
    public async Task ASecondRunWhileOneIsActiveIsRejectedAndTheLockIsReleasedAfterwards()
    {
        await CreateYearAsync(2015);
        await using var holder = await _dataSource!.OpenConnectionAsync();
        await using (var take = new NpgsqlCommand("SELECT pg_advisory_lock(@key);", holder))
        {
            take.Parameters.AddWithValue("key", AuditPartitionDisposal.AdvisoryLockKey);
            await take.ExecuteNonQueryAsync();
        }

        var disposal = new AuditPartitionDisposal(_dataSource!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => disposal.RunAsync(Now, apply: true, operatorName: "qa.operator"));
        Assert.True(await ScalarAsync<bool>("SELECT to_regclass('audit.audit_events_y2015') IS NOT NULL;"));

        await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", holder))
        {
            release.Parameters.AddWithValue("key", AuditPartitionDisposal.AdvisoryLockKey);
            await release.ExecuteNonQueryAsync();
        }

        var result = await disposal.RunAsync(Now, apply: true, operatorName: "qa.operator");
        Assert.Single(result.Dropped);
        var again = await disposal.RunAsync(Now, apply: false);
        Assert.Empty(again.Dropped);
    }

    [Fact]
    public async Task TheCommandSaysInTurkishThatAnotherRunIsActive()
    {
        await using var holder = await _dataSource!.OpenConnectionAsync();
        await using (var take = new NpgsqlCommand("SELECT pg_advisory_lock(@key);", holder))
        {
            take.Parameters.AddWithValue("key", AuditPartitionDisposal.AdvisoryLockKey);
            await take.ExecuteNonQueryAsync();
        }

        var exit = RunCommand(["audit-disposal", "--db-url", _database.Url], out _, out var err);

        Assert.Equal((int)HostExitCode.StartupFailed, exit);
        Assert.Contains("başka bir imha çalışması sürüyor", err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDroppedRowCountIsTheOneRecordedInTheEvent()
    {
        await CreateYearAsync(2015);
        await InsertAsync("2015-03-03", "2015-07-07", "2015-09-09");

        var result = await new AuditPartitionDisposal(_dataSource!).RunAsync(Now, apply: true, operatorName: "qa.operator");

        Assert.Equal(3L, result.Dropped.Single().Rows);
        Assert.Equal(3L, await ScalarAsync<long>("SELECT (metadata_json->>'rows')::bigint FROM audit.audit_events WHERE event_name = 'audit.partition.disposed';"));
    }

    [Fact]
    public async Task TheCommandReportsRowsInTheDefaultPartitionWithAWarningExitCode()
    {
        var cleanExit = RunCommand(["audit-disposal", "--db-url", _database.Url], out var cleanOut, out _);
        Assert.Equal((int)HostExitCode.Success, cleanExit);
        Assert.Contains("default_partition_rows=0", cleanOut, StringComparison.Ordinal);

        await InsertAsync("1999-01-01");
        var warnExit = RunCommand(["audit-disposal", "--db-url", _database.Url], out var warnOut, out var warnErr);

        Assert.Equal((int)HostExitCode.CompletedWithWarnings, warnExit);
        Assert.Contains("default_partition_rows=1", warnOut, StringComparison.Ordinal);
        Assert.Contains("UYARI", warnErr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--apply", "--operator")]
    [InlineData("--unknown", "Tanınmayan")]
    public void CommandErrorsAreInTurkishAndNameTheOption(string argument, string expected)
    {
        var exit = RunCommand(["audit-disposal", "--db-url", _database.Url, argument], out _, out var err);

        Assert.Equal((int)HostExitCode.StartupFailed, exit);
        Assert.Contains("AUDIT-DISPOSAL", err, StringComparison.Ordinal);
        Assert.Contains(expected, err, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnexpectedFailureNeverPrintsTheRawExceptionTextAndPointsAtTheRunbook()
    {
        var exit = RunCommand(["audit-disposal", "--db-url", "postgresql://nobody@127.0.0.1:1/none"], out _, out var err);

        Assert.Equal((int)HostExitCode.StartupFailed, exit);
        Assert.Contains("kvkk-retention-runbook.md", err, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", err, StringComparison.Ordinal);
        Assert.DoesNotContain("nobody", err, StringComparison.Ordinal);
    }

    private static int RunCommand(string[] args, out string stdout, out string stderr)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            return ALKAROS.Host.Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            stdout = outWriter.ToString();
            stderr = errWriter.ToString();
        }
    }

    private Task CreateYearAsync(int year)
        => ExecuteAsync(
            $"CREATE TABLE audit.audit_events_y{year} PARTITION OF audit.audit_events FOR VALUES FROM ('{year}-01-01 00:00:00+00') TO ('{year + 1}-01-01 00:00:00+00');");

    private Task InsertAsync(params string[] occurredAt)
        => ExecuteAsync(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_type, correlation_id, occurred_at)
            SELECT gen_random_uuid(), 'test.event', 'test', gen_random_uuid(), 'system', 'corr', (d || ' 00:00:00+00')::timestamptz
            FROM unnest(@dates) AS d;
            """,
            ("dates", occurredAt));

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private static string FindRepositoryRoot()
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
