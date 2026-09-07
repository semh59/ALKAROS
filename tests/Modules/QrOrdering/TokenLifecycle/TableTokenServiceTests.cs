namespace ALKAROS.QrOrdering.TokenLifecycle.Tests;

using ALKAROS.QrOrdering.TokenLifecycle.Tests.Fixtures;
using NpgsqlTypes;
using Xunit;

/// <summary>
/// V14-QRS-001, against a real Postgres database created from
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
    public async Task ADefaultIssuedTokenExpiresFourHoursAfterIssuance()
    {
        var tableId = await _database.SeedTableAsync();
        var before = DateTimeOffset.UtcNow;
        await _service.IssueAsync(tableId);
        var after = DateTimeOffset.UtcNow;

        var active = await _repository.GetActiveForTableAsync(tableId);
        Assert.NotNull(active);
        Assert.InRange(active!.ExpiresAt - active.IssuedAt, TimeSpan.FromHours(4), TimeSpan.FromHours(4));
        Assert.InRange(active.IssuedAt, before, after);
    }
}
