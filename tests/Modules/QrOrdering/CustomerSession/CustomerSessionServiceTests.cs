namespace ALKAROS.QrOrdering.CustomerSession.Tests;

using ALKAROS.QrOrdering.CustomerSession.Tests.Fixtures;
using ALKAROS.QrOrdering.TokenLifecycle;
using NpgsqlTypes;
using Xunit;

/// <summary>
/// V12-QRS-003, against a real Postgres database created from
/// 010-tables.up.sql + 078-qr-ordering-table-tokens.up.sql +
/// 085-qr-ordering-customer-sessions.up.sql.
/// </summary>
public sealed class CustomerSessionServiceTests : IClassFixture<QrOrderingCustomerSessionTestDatabase>
{
    private readonly QrOrderingCustomerSessionTestDatabase _database;
    private readonly TableTokenService _tableTokenService;
    private readonly PostgresCustomerSessionRepository _repository;
    private readonly CustomerSessionService _service;

    public CustomerSessionServiceTests(QrOrderingCustomerSessionTestDatabase database)
    {
        _database = database;
        _tableTokenService = new TableTokenService(new PostgresTableTokenRepository(database.DataSource));
        _repository = new PostgresCustomerSessionRepository(database.DataSource);
        _service = new CustomerSessionService(_repository, _tableTokenService);
    }

    [Fact]
    public async Task IssuingFromAValidTableTokenReturnsASessionScopedToItsTable()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);

        var result = await _service.IssueAsync(rawTableToken);

        Assert.True(result.IsValid);
        Assert.Equal(tableId, result.TableId);
        Assert.NotNull(result.RawToken);
        Assert.NotNull(result.SessionId);
    }

    [Fact]
    public async Task IssuingFromAnUnknownTableTokenFailsWithTheUnderlyingReason()
    {
        var result = await _service.IssueAsync("alkaros-table-token:this-was-never-issued");

        Assert.False(result.IsValid);
        Assert.Equal("NOT_FOUND", result.FailureReason);
        Assert.Null(result.RawToken);
    }

    [Fact]
    public async Task IssuingFromARevokedTableTokenFails()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        await _tableTokenService.RevokeAsync(tableId, "table closed early");

        var result = await _service.IssueAsync(rawTableToken);

        Assert.False(result.IsValid);
        Assert.Equal("REVOKED", result.FailureReason);
    }

    [Fact]
    public async Task AnIssuedSessionValidatesToItsOwnTable()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        var result = await _service.ValidateAsync(issued.RawToken!);

        Assert.True(result.IsValid);
        Assert.Equal(tableId, result.TableId);
        Assert.Equal(issued.SessionId, result.SessionId);
    }

    [Fact]
    public async Task ADatabaseLeakExposesNoUsableRawToken()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        await using var cmd = _database.DataSource.CreateCommand(
            "SELECT token_hash FROM qr_ordering.customer_sessions WHERE session_id = @session_id;");
        cmd.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = issued.SessionId!.Value;
        var storedHash = (string)(await cmd.ExecuteScalarAsync())!;

        Assert.NotEqual(issued.RawToken, storedHash);
        Assert.DoesNotContain(issued.RawToken!, storedHash, StringComparison.Ordinal);
        Assert.DoesNotContain(storedHash, issued.RawToken!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownSessionFailsValidationAsNotFound()
    {
        var result = await _service.ValidateAsync("alkaros-customer-session:this-was-never-issued");
        Assert.False(result.IsValid);
        Assert.Equal("NOT_FOUND", result.FailureReason);
    }

    [Fact]
    public async Task ARevokedSessionFailsValidation()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        await _service.RevokeAsync(issued.SessionId!.Value, "customer left");
        var result = await _service.ValidateAsync(issued.RawToken!);

        Assert.False(result.IsValid);
        Assert.Equal("REVOKED", result.FailureReason);
    }

    [Fact]
    public async Task RevokingAnUnknownSessionThrows()
    {
        await Assert.ThrowsAsync<CustomerSessionNotFoundException>(() => _service.RevokeAsync(Guid.NewGuid(), "n/a"));
    }

    [Fact]
    public async Task RevokingAnAlreadyRevokedSessionThrows()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        await _service.RevokeAsync(issued.SessionId!.Value, "first");
        await Assert.ThrowsAsync<CustomerSessionNotFoundException>(() => _service.RevokeAsync(issued.SessionId!.Value, "second"));
    }

    [Fact]
    public async Task AnAbsolutelyExpiredSessionFailsValidation()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        // Backdoor the row's absolute expiry into the near future rather than
        // the past: the constructor rejects expiresAt <= issuedAt on every
        // read, so 1ms after issuance is enough for real time to have passed
        // it by the time the next call runs (same technique as
        // TableTokenServiceTests.AnExpiredTokenFailsValidation).
        await using var cmd = _database.DataSource.CreateCommand(
            "UPDATE qr_ordering.customer_sessions SET expires_at = issued_at + interval '1 millisecond' WHERE session_id = @session_id;");
        cmd.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = issued.SessionId!.Value;
        await cmd.ExecuteNonQueryAsync();

        var result = await _service.ValidateAsync(issued.RawToken!);
        Assert.False(result.IsValid);
        Assert.Equal("EXPIRED", result.FailureReason);
    }

    [Fact]
    public async Task AnIdleExpiredSessionFailsValidation()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        // Backdoor both issued_at and last_activity_at far enough into the
        // past that the service's fixed 30-minute idle timeout has already
        // elapsed, while keeping expires_at (the absolute lifetime, set at
        // issuance to 4 hours out) untouched — isolates the idle-timeout path
        // from the absolute-expiry path. issued_at must move back too: the
        // domain constructor rejects lastActivityAt < issuedAt on every read.
        await using var cmd = _database.DataSource.CreateCommand(
            "UPDATE qr_ordering.customer_sessions SET issued_at = now() - interval '35 minutes', last_activity_at = now() - interval '31 minutes' WHERE session_id = @session_id;");
        cmd.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = issued.SessionId!.Value;
        await cmd.ExecuteNonQueryAsync();

        var result = await _service.ValidateAsync(issued.RawToken!);
        Assert.False(result.IsValid);
        Assert.Equal("IDLE_EXPIRED", result.FailureReason);
    }

    [Fact]
    public async Task ValidatingASessionSlidesItsIdleWindowForward()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var issued = await _service.IssueAsync(rawTableToken);

        // Push issued_at and last_activity_at into the past together — close
        // to, but not past, the idle timeout — then validate; a successful
        // validation must slide the window forward so a second, immediate
        // validation still passes. issued_at moves back too: the domain
        // constructor rejects lastActivityAt < issuedAt on every read.
        await using var backdoor = _database.DataSource.CreateCommand(
            "UPDATE qr_ordering.customer_sessions SET issued_at = now() - interval '35 minutes', last_activity_at = now() - interval '29 minutes' WHERE session_id = @session_id;");
        backdoor.Parameters.Add("session_id", NpgsqlDbType.Uuid).Value = issued.SessionId!.Value;
        await backdoor.ExecuteNonQueryAsync();

        var first = await _service.ValidateAsync(issued.RawToken!);
        Assert.True(first.IsValid);

        var session = await _repository.GetByHashAsync(CustomerSessionTokenGenerator.Hash(issued.RawToken!));
        Assert.NotNull(session);
        Assert.InRange(DateTimeOffset.UtcNow - session!.LastActivityAt, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        var second = await _service.ValidateAsync(issued.RawToken!);
        Assert.True(second.IsValid);
    }

    [Fact]
    public async Task ADefaultIssuedSessionExpiresFourHoursAfterIssuance()
    {
        var tableId = await _database.SeedTableAsync();
        var rawTableToken = await _tableTokenService.IssueAsync(tableId);
        var before = DateTimeOffset.UtcNow;
        var issued = await _service.IssueAsync(rawTableToken);
        var after = DateTimeOffset.UtcNow;

        var session = await _repository.GetByHashAsync(CustomerSessionTokenGenerator.Hash(issued.RawToken!));
        Assert.NotNull(session);
        Assert.InRange(session!.ExpiresAt - session.IssuedAt, TimeSpan.FromHours(4), TimeSpan.FromHours(4));
        Assert.InRange(session.IssuedAt, before, after);
    }
}
