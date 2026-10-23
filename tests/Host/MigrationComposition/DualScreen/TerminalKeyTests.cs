using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

// A terminal id only names a terminal; the browser's secret key proves it is the same browser.
[Collection("Host database password environment")]
public sealed class TerminalKeyTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private NpgsqlDataSource? _dataSource;
    private DualScreenStore? _store;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var root = FindRepositoryRoot();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(root, "database", "MigrationComposition", "order.json"),
                Path.Combine(root, "database", "migrations"),
                _database.PsqlOptions),
            TextWriter.Null);
        Assert.Equal(HostExitCode.Success, exit);
        _dataSource = NpgsqlDataSource.Create(CreateConnectionString());
        _store = new DualScreenStore(_dataSource);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task TheFirstLoginClaimsTheTerminalAndOnlyTheHashIsStored()
    {
        var terminalId = Guid.NewGuid();

        var (result, key) = await _store!.ClaimTerminalKeyAsync(terminalId, null, CancellationToken.None);

        Assert.Equal(TerminalKeyResult.Claimed, result);
        Assert.False(string.IsNullOrEmpty(key));
        var stored = await ScalarAsync<string>(
            "SELECT terminal_key_hash FROM customer_display.terminals WHERE terminal_id = @id;", terminalId);
        Assert.Equal(DualScreenToken.Hash(key!), stored);
        Assert.DoesNotContain(key!, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyTheBrowserHoldingTheKeyIsAcceptedAgain()
    {
        var terminalId = Guid.NewGuid();
        var (_, key) = await _store!.ClaimTerminalKeyAsync(terminalId, null, CancellationToken.None);

        var same = await _store.ClaimTerminalKeyAsync(terminalId, key, CancellationToken.None);
        var missing = await _store.ClaimTerminalKeyAsync(terminalId, null, CancellationToken.None);
        var wrong = await _store.ClaimTerminalKeyAsync(terminalId, "tk-not-the-key", CancellationToken.None);

        Assert.Equal((TerminalKeyResult.Accepted, null), same);
        Assert.Equal((TerminalKeyResult.Rejected, null), missing);
        Assert.Equal((TerminalKeyResult.Rejected, null), wrong);
    }

    [Fact]
    public async Task ATerminalThatExistedBeforeKeysAdoptsTheKeyOfItsNextLogin()
    {
        var terminalId = Guid.NewGuid();
        await _store!.EnsureTerminalAsync(terminalId, CancellationToken.None);
        Assert.Equal(0, await ScalarAsync<long>(
            "SELECT count(*) FROM customer_display.terminals WHERE terminal_id = @id AND terminal_key_hash IS NOT NULL;", terminalId));

        var (result, key) = await _store.ClaimTerminalKeyAsync(terminalId, "tk-stale-cookie", CancellationToken.None);

        Assert.Equal(TerminalKeyResult.Claimed, result);
        Assert.Equal(TerminalKeyResult.Accepted, (await _store.ClaimTerminalKeyAsync(terminalId, key, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task TwoBrowsersClaimingTheSameNewTerminalAtOnceLeaveExactlyOneWinner()
    {
        var terminalId = Guid.NewGuid();
        await _store!.EnsureTerminalAsync(terminalId, CancellationToken.None);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _store.ClaimTerminalKeyAsync(terminalId, null, CancellationToken.None)));

        Assert.Single(results, outcome => outcome.Result == TerminalKeyResult.Claimed);
        Assert.Equal(7, results.Count(outcome => outcome.Result == TerminalKeyResult.Rejected));
    }

    [Fact]
    public async Task AResetReleasesTheTerminalAndTheOldKeyStopsWorking()
    {
        var terminalId = Guid.NewGuid();
        var (_, oldKey) = await _store!.ClaimTerminalKeyAsync(terminalId, null, CancellationToken.None);
        var audited = 0;

        var reset = await _store.ResetTerminalKeyAsync(terminalId, (_, _, _) => { audited++; return Task.CompletedTask; }, CancellationToken.None);

        Assert.True(reset);
        Assert.Equal(1, audited);
        var (result, newKey) = await _store.ClaimTerminalKeyAsync(terminalId, oldKey, CancellationToken.None);
        Assert.Equal(TerminalKeyResult.Claimed, result);
        Assert.NotEqual(oldKey, newKey);
        Assert.Equal(TerminalKeyResult.Rejected, (await _store.ClaimTerminalKeyAsync(terminalId, oldKey, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task AFailedAuditRollsTheResetBackAndAnUnknownTerminalIsReportedAsMissing()
    {
        var terminalId = Guid.NewGuid();
        var (_, key) = await _store!.ClaimTerminalKeyAsync(terminalId, null, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.ResetTerminalKeyAsync(
            terminalId, (_, _, _) => throw new InvalidOperationException("audit refused"), CancellationToken.None));

        Assert.Equal(TerminalKeyResult.Accepted, (await _store.ClaimTerminalKeyAsync(terminalId, key, CancellationToken.None)).Result);
        Assert.False(await _store.ResetTerminalKeyAsync(Guid.NewGuid(), (_, _, _) => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task TheListShowsWhichTerminalsAreBoundAndWhoSignedInLast()
    {
        var bound = Guid.NewGuid();
        var open = Guid.NewGuid();
        await _store!.ClaimTerminalKeyAsync(bound, null, CancellationToken.None);
        await _store.EnsureTerminalAsync(open, CancellationToken.None);
        var userId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.users (user_id, username, password_hash, display_name, active) VALUES (@user, @name, 'unused', 'Ayşe Kasiyer', TRUE);",
            ("user", userId), ("name", $"list-{userId:N}"));
        await ExecuteAsync(
            "INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at) VALUES (@session, @user, @device, @hash, now(), now() + interval '1 hour');",
            ("session", Guid.NewGuid()), ("user", userId), ("device", $"cashier:{bound:D}"), ("hash", DualScreenToken.Hash(Guid.NewGuid().ToString("N"))));

        var terminals = await _store.ListTerminalsAsync(CancellationToken.None);

        var boundRow = Assert.Single(terminals, terminal => terminal.TerminalId == bound);
        Assert.True(boundRow.Bound);
        Assert.Equal("Ayşe Kasiyer", boundRow.LastUserName);
        Assert.NotNull(boundRow.LastLoginAt);
        var openRow = Assert.Single(terminals, terminal => terminal.TerminalId == open);
        Assert.False(openRow.Bound);
        Assert.Null(openRow.LastUserName);
    }

    [Fact]
    public async Task ThePersistedKeyHashMustLookLikeAHash()
    {
        var terminalId = Guid.NewGuid();
        await _store!.EnsureTerminalAsync(terminalId, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "UPDATE customer_display.terminals SET terminal_key_hash = 'plain-key' WHERE terminal_id = @id;", ("id", terminalId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, Guid id)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private string CreateConnectionString()
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
