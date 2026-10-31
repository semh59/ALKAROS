namespace ALKAROS.QrOrdering.TokenLifecycle.Tests;

using ALKAROS.QrOrdering.TokenLifecycle.Tests.Fixtures;
using NpgsqlTypes;
using Xunit;

/// <summary>
/// V12-QRS-001, against a real Postgres database created from
/// 010-tables.up.sql + 078-qr-ordering-table-tokens.up.sql.
/// </summary>
public sealed class TableTokenServiceTests : IClassFixture<QrOrderingTokenLifecycleTestDatabase>
{
    private readonly QrOrderingTokenLifecycleTestDatabase _database;
    private readonly PostgresTableTokenRepository _repository;
    private readonly TableTokenService _service;

    public TableTokenServiceTests(QrOrderingTokenLifecycleTestDatabase database)
    {
        _database = database;
        _repository = new PostgresTableTokenRepository(database.DataSource);
        _service = new TableTokenService(_repository);
    }

    [Fact]
    public async Task IssuedTokenValidatesToItsOwnTable()
    {
        var tableId = await _database.SeedTableAsync();

        var raw = await _service.IssueAsync(tableId);
        var result = await _service.ValidateAsync(raw);

        Assert.True(result.IsValid);
        Assert.Equal(tableId, result.TableId);
    }

    [Fact]
    public async Task ADatabaseLeakExposesNoUsableRawToken()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _service.IssueAsync(tableId);

        await using var cmd = _database.DataSource.CreateCommand(
            "SELECT token_hash FROM qr_ordering.table_tokens WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        var storedHash = (string)(await cmd.ExecuteScalarAsync())!;

        // The stored column is never the raw token itself, in whole or in
        // part — a leaked row cannot be replayed as a bearer credential.
        Assert.NotEqual(raw, storedHash);
        Assert.DoesNotContain(raw, storedHash, StringComparison.Ordinal);
        Assert.DoesNotContain(storedHash, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssuingASecondTokenForATableThatAlreadyHasOneThrows()
    {
        var tableId = await _database.SeedTableAsync();
        await _service.IssueAsync(tableId);

        await Assert.ThrowsAsync<TableTokenAlreadyActiveException>(() => _service.IssueAsync(tableId));
    }

    [Fact]
    public async Task ConcurrentIssuanceForTheSameTableLetsExactlyOneWin()
    {
        var tableId = await _database.SeedTableAsync();

        var first = _service.IssueAsync(tableId);
        var second = _service.IssueAsync(tableId);

        var results = await Task.WhenAll(
            first.ContinueWith(t => t.Exception is null),
            second.ContinueWith(t => t.Exception is null));

        Assert.Single(results, ok => ok);
        Assert.Equal(1L, await _database.ActiveTokenCountAsync(tableId));
    }

    [Fact]
    public async Task AnUnknownTokenFailsValidationAsNotFound()
    {
        var result = await _service.ValidateAsync("alkaros-table-token:this-was-never-issued");
        Assert.False(result.IsValid);
        Assert.Equal("NOT_FOUND", result.FailureReason);
    }

    [Fact]
    public async Task ARevokedTokenFailsValidation()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _service.IssueAsync(tableId);

        await _service.RevokeAsync(tableId, "table closed early");
        var result = await _service.ValidateAsync(raw);

        Assert.False(result.IsValid);
        Assert.Equal("REVOKED", result.FailureReason);
    }

    [Fact]
    public async Task RevokingATableWithNoActiveTokenThrows()
    {
        var tableId = await _database.SeedTableAsync();
        await Assert.ThrowsAsync<TableTokenNotFoundException>(() => _service.RevokeAsync(tableId, "n/a"));
    }

    [Fact]
    public async Task AnExpiredTokenFailsValidation()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _service.IssueAsync(tableId);

        // Backdoor the row's expiry into the near past directly — waiting
        // out a real 4-hour lifetime in a test would be absurd. Set it just
        // 1ms after issuance rather than into the actual past: TableToken's
        // own constructor rejects expiresAt <= issuedAt, and this row gets
        // reconstructed through that same constructor on every read, so an
        // expiry earlier than issuance would fail to even load. By the time
        // this UPDATE's round trip and the next call complete, real time has
        // already passed that 1ms mark.
        await using var cmd = _database.DataSource.CreateCommand(
            "UPDATE qr_ordering.table_tokens SET expires_at = issued_at + interval '1 millisecond' WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await cmd.ExecuteNonQueryAsync();

        var result = await _service.ValidateAsync(raw);
        Assert.False(result.IsValid);
        Assert.Equal("EXPIRED", result.FailureReason);
    }

    [Fact]
    public async Task RotatingInvalidatesThePreviousTokenAndTheNewOneWorks()
    {
        var tableId = await _database.SeedTableAsync();
        var oldRaw = await _service.IssueAsync(tableId);

        var newRaw = await _service.RotateAsync(tableId);

        var oldResult = await _service.ValidateAsync(oldRaw);
        Assert.False(oldResult.IsValid);
        Assert.Equal("REVOKED", oldResult.FailureReason);

        var newResult = await _service.ValidateAsync(newRaw);
        Assert.True(newResult.IsValid);
        Assert.Equal(tableId, newResult.TableId);

        // Rotation is atomic: never zero, never two active rows.
        Assert.Equal(1L, await _database.ActiveTokenCountAsync(tableId));
    }

    [Fact]
    public async Task RotatingATableWithNoActiveTokenThrows()
    {
        var tableId = await _database.SeedTableAsync();
        await Assert.ThrowsAsync<TableTokenNotFoundException>(() => _service.RotateAsync(tableId));
    }

    [Fact]
    public async Task ADefaultIssuedTokenStaysValidForTenYearsLikeAPrintedLabel()
    {
        var tableId = await _database.SeedTableAsync();
        var before = DateTimeOffset.UtcNow;
        await _service.IssueAsync(tableId);
        var after = DateTimeOffset.UtcNow;

        var active = await _repository.GetActiveForTableAsync(tableId);
        Assert.NotNull(active);
        Assert.Equal(TimeSpan.FromDays(3650), active!.ExpiresAt - active.IssuedAt);
        Assert.InRange(active.IssuedAt, before, after);
        Assert.False(active.IsExpired(DateTimeOffset.UtcNow.AddHours(5)));
    }

    [Fact]
    public async Task IssuingForATableWhoseTokenTimedOutReplacesItInsteadOfRefusing()
    {
        var tableId = await _database.SeedTableAsync();
        var shortLived = new TableTokenService(_repository, TimeSpan.FromMilliseconds(1));
        var staleRaw = await shortLived.IssueAsync(tableId);
        await Task.Delay(20);

        var freshRaw = await _service.IssueAsync(tableId);

        var stale = await _service.ValidateAsync(staleRaw);
        Assert.False(stale.IsValid);
        Assert.Equal("REVOKED", stale.FailureReason);
        Assert.True((await _service.ValidateAsync(freshRaw)).IsValid);
        Assert.Equal(1L, await _database.ActiveTokenCountAsync(tableId));
    }

    [Fact]
    public async Task RevokingWithABlankReasonIsRejectedBeforeAnythingChanges()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _service.IssueAsync(tableId);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.RevokeAsync(tableId, " "));

        Assert.True((await _service.ValidateAsync(raw)).IsValid);
    }

    [Fact]
    public async Task ListActiveReturnsEachTablesCurrentTokenAndNotARevokedOne()
    {
        var withToken = await _database.SeedTableAsync();
        var revoked = await _database.SeedTableAsync();
        await _service.IssueAsync(withToken);
        await _service.IssueAsync(revoked);
        await _service.RevokeAsync(revoked, "test");

        var tableIds = (await _repository.ListActiveAsync()).Select(token => token.TableId).ToList();

        Assert.Contains(withToken, tableIds);
        Assert.DoesNotContain(revoked, tableIds);
    }

    [Fact]
    public async Task LosingTheRaceToReplaceATimedOutTokenReportsAlreadyActiveNotMissing()
    {
        var tableId = Guid.NewGuid();
        var issued = DateTimeOffset.UtcNow.AddHours(-2);
        var timedOut = new TableToken(Guid.NewGuid(), tableId, "hash", issued, issued.AddHours(1));
        var service = new TableTokenService(new RacedRepository(timedOut));

        await Assert.ThrowsAsync<TableTokenAlreadyActiveException>(() => service.IssueAsync(tableId));
    }

    /// <summary>Reports a timed-out token, then loses the rotation to a concurrent caller.</summary>
    private sealed class RacedRepository(TableToken current) : ITableTokenRepository
    {
        public Task<TableToken?> GetActiveForTableAsync(Guid tableId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TableToken?>(current);

        public Task RotateAsync(Guid previousTokenId, string revokedReason, TableToken newToken, CancellationToken cancellationToken = default) =>
            throw new TableTokenNotFoundException("already replaced");

        public Task<TableToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<TableToken>> ListActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(TableToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RevokeAsync(Guid tokenId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
