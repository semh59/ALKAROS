namespace ALKAROS.QrOrdering.RelaySecurity.Tests;

using ALKAROS.QrOrdering.RelaySecurity.Tests.Fixtures;
using ALKAROS.QrOrdering.TokenLifecycle;
using Xunit;

/// <summary>
/// V12-QRS-002, against a real Postgres database. Every scenario goes
/// through the same table token a real QR/relay request would carry
/// (V12-QRS-001), so this exercises the full "token → nonce → timestamp"
/// chain, not each piece in isolation.
/// </summary>
public sealed class RelayRequestValidatorTests : IClassFixture<RelaySecurityTestDatabase>
{
    private readonly RelaySecurityTestDatabase _database;
    private readonly TableTokenService _tokens;
    private readonly RelayRequestValidator _validator;

    public RelayRequestValidatorTests(RelaySecurityTestDatabase database)
    {
        _database = database;
        var tokenRepository = new PostgresTableTokenRepository(database.DataSource);
        _tokens = new TableTokenService(tokenRepository);
        var nonceStore = new PostgresRelayNonceStore(database.DataSource);
        _validator = new RelayRequestValidator(_tokens, nonceStore);
    }

    [Fact]
    public async Task AFreshRequestWithAValidTokenAndNonceIsAccepted()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _tokens.IssueAsync(tableId);

        var result = await _validator.ValidateAsync(raw, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Equal(tableId, result.TableId);
    }

    [Fact]
    public async Task AnUnknownTokenIsRejectedBeforeTheNonceOrTimestampIsEvenChecked()
    {
        var result = await _validator.ValidateAsync("alkaros-table-token:never-issued", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Equal("NOT_FOUND", result.FailureReason);
    }

    [Fact]
    public async Task TheSameNonceUsedTwiceForTheSameTokenIsRejectedTheSecondTime()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _tokens.IssueAsync(tableId);
        var nonce = Guid.NewGuid();

        var first = await _validator.ValidateAsync(raw, nonce, DateTimeOffset.UtcNow);
        var second = await _validator.ValidateAsync(raw, nonce, DateTimeOffset.UtcNow);

        Assert.True(first.IsValid);
        Assert.False(second.IsValid);
        Assert.Equal("REPLAYED", second.FailureReason);
    }

    [Fact]
    public async Task TheSameNonceValueIsIndependentAcrossDifferentTokens()
    {
        var firstTableId = await _database.SeedTableAsync();
        var secondTableId = await _database.SeedTableAsync();
        var firstRaw = await _tokens.IssueAsync(firstTableId);
        var secondRaw = await _tokens.IssueAsync(secondTableId);
        var sharedNonce = Guid.NewGuid();

        var first = await _validator.ValidateAsync(firstRaw, sharedNonce, DateTimeOffset.UtcNow);
        var second = await _validator.ValidateAsync(secondRaw, sharedNonce, DateTimeOffset.UtcNow);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
    }

    [Fact]
    public async Task ATimestampTooFarInThePastIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _tokens.IssueAsync(tableId);

        var result = await _validator.ValidateAsync(raw, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-10));

        Assert.False(result.IsValid);
        Assert.Equal("TIMESTAMP_OUT_OF_WINDOW", result.FailureReason);
    }

    [Fact]
    public async Task ATimestampTooFarInTheFutureIsRejected()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _tokens.IssueAsync(tableId);

        var result = await _validator.ValidateAsync(raw, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.False(result.IsValid);
        Assert.Equal("TIMESTAMP_OUT_OF_WINDOW", result.FailureReason);
    }

    [Fact]
    public async Task ARevokedTokenIsRejectedWithTheTokenServicesOwnReason()
    {
        var tableId = await _database.SeedTableAsync();
        var raw = await _tokens.IssueAsync(tableId);
        await _tokens.RevokeAsync(tableId, "test revocation");

        var result = await _validator.ValidateAsync(raw, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Equal("REVOKED", result.FailureReason);
    }
}
